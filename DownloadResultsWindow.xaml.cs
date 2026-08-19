using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NavisWebAppSync
{
    public partial class DownloadResultsWindow : Window
    {
        private readonly BinaConfig _config;
        private readonly string _downloadPath;
        private readonly ObservableCollection<DownloadItemViewModel> _downloadItems;

        public DownloadResultsWindow(BinaConfig config, string downloadPath)
        {
            InitializeComponent();
            _config = config;
            _downloadPath = downloadPath;
            _downloadItems = new ObservableCollection<DownloadItemViewModel>();
            ResultsList.ItemsSource = _downloadItems;

            Loaded += async (s, e) => await StartDownloadAsync();
        }

        /// <summary>
        /// Presigned URLs expire after 1h (OBS_EXPIRY_TIME=3600). Re-fetch the list before
        /// they go stale if a large multi-folder pull is still running.
        /// </summary>
        private static readonly TimeSpan UrlRefreshAfter = TimeSpan.FromMinutes(45);

        private DateTime _listFetchedAtUtc;

        /// <summary>
        /// One row in the results list: either a downloadable file or an explicit notice.
        /// </summary>
        private class PullEntry
        {
            public string DisciplineType;
            public string FolderName;
            public BimLatestFile File;    // null when there is nothing to download
            public string DisplayName;    // shown in the FileName column
            public string Error;          // non-null => notice row, never downloaded
        }

        private static string EntryKey(PullEntry entry)
        {
            return string.Join("|",
                entry.DisciplineType,
                entry.FolderName,
                entry.File?.Id.ToString() ?? "-",
                entry.File?.FileName ?? "-");
        }

        private static string FileTypeOf(BimLatestFile file)
        {
            string type = file?.FileType;
            if (string.IsNullOrEmpty(type))
            {
                type = Path.GetExtension(file?.FileName ?? "").TrimStart('.');
            }
            return type ?? "";
        }

        /// <summary>
        /// True when the backend returned the source .rvt because no .nwc was linked.
        /// </summary>
        private static bool IsRevitFallback(BimLatestFile file)
        {
            string type = FileTypeOf(file);
            return !string.IsNullOrEmpty(type)
                && !type.Equals("nwc", StringComparison.OrdinalIgnoreCase);
        }

        private static string FileTypeLabel(BimLatestFile file)
        {
            string type = FileTypeOf(file);
            return string.IsNullOrEmpty(type) ? "" : type.ToUpperInvariant();
        }

        private List<PullEntry> GetFilesToDownload(BimDisciplineResponse disciplineFiles)
        {
            var filesToDownload = new List<PullEntry>();

            void AddNotice(string disciplineName, string folderName, string displayName, string error)
            {
                filesToDownload.Add(new PullEntry
                {
                    DisciplineType = disciplineName,
                    FolderName = folderName,
                    File = null,
                    DisplayName = displayName,
                    Error = error
                });
            }

            void AddDisciplineFiles(string disciplineName, BimDiscipline discipline)
            {
                if (discipline?.Folders == null) return;

                foreach (var folder in discipline.Folders)
                {
                    if (folder == null) continue;

                    if (folder.Files != null && folder.Files.Count > 0)
                    {
                        foreach (var file in folder.Files)
                        {
                            if (file == null)
                            {
                                AddNotice(disciplineName, folder.Name, "(empty entry)",
                                    "Server returned an empty file entry.");
                            }
                            else if (!string.IsNullOrEmpty(file.Error))
                            {
                                // Errors are reported per file by the backend, not per folder.
                                AddNotice(disciplineName, folder.Name, file.FileName ?? "(no file)", file.Error);
                            }
                            else if (string.IsNullOrEmpty(file.FileUrl))
                            {
                                AddNotice(disciplineName, folder.Name, file.FileName ?? "(no file)",
                                    $"No download URL for {file.FileName ?? "file"}.");
                            }
                            else
                            {
                                filesToDownload.Add(new PullEntry
                                {
                                    DisciplineType = disciplineName,
                                    FolderName = folder.Name,
                                    File = file,
                                    DisplayName = file.FileName,
                                    Error = null
                                });
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(folder.Error))
                    {
                        AddNotice(disciplineName, folder.Name, "(no file)", folder.Error);
                    }
                    else
                    {
                        // Never skip silently - an empty pull has to be visible in the UI.
                        AddNotice(disciplineName, folder.Name, "(no file)",
                            "Server returned no files for this folder.");
                    }
                }
            }

            AddDisciplineFiles("Structure", disciplineFiles.Structure);
            AddDisciplineFiles("Architecture", disciplineFiles.Architecture);
            AddDisciplineFiles("Mechanical", disciplineFiles.Mechanical);
            AddDisciplineFiles("Electrical", disciplineFiles.Electrical);

            return filesToDownload;
        }

        /// <summary>
        /// Re-fetch the list and swap in fresh presigned URLs for entries not downloaded yet.
        /// </summary>
        private async Task RefreshUrlsAsync(List<PullEntry> entries)
        {
            var fresh = await BinaApiService.GetBimDisciplineFilesAsync(
                _config.ProjectId, _config.AccessToken);

            if (fresh == null) return;

            var freshUrls = new Dictionary<string, string>();
            foreach (var entry in GetFilesToDownload(fresh))
            {
                if (entry.File != null && !string.IsNullOrEmpty(entry.File.FileUrl))
                {
                    freshUrls[EntryKey(entry)] = entry.File.FileUrl;
                }
            }

            foreach (var entry in entries)
            {
                if (entry.File == null) continue;

                string url;
                if (freshUrls.TryGetValue(EntryKey(entry), out url))
                {
                    entry.File.FileUrl = url;
                }
            }

            _listFetchedAtUtc = DateTime.UtcNow;
        }

        private async Task StartDownloadAsync()
        {
            try
            {
                ProgressText.Text = "Fetching file list from server...";
                ProgressBar.IsIndeterminate = true;

                var disciplineFiles = await BinaApiService.GetBimDisciplineFilesAsync(
                    _config.ProjectId, _config.AccessToken);

                if (disciplineFiles == null)
                {
                    HeaderText.Text = "Download Failed";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Red;
                    ProgressText.Text = "Failed to fetch file list from server.";
                    ProgressBar.IsIndeterminate = false;
                    CloseButton.IsEnabled = true;
                    return;
                }

                _listFetchedAtUtc = DateTime.UtcNow;

                var filesToDownload = GetFilesToDownload(disciplineFiles);

                if (filesToDownload.Count == 0)
                {
                    HeaderText.Text = "No Files Available";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Orange;
                    ProgressText.Text = "Server returned no discipline folders for this project.";
                    ProgressBar.IsIndeterminate = false;
                    CloseButton.IsEnabled = true;
                    return;
                }

                // Initialize download items
                int noticeCount = 0;
                foreach (var entry in filesToDownload)
                {
                    if (entry.Error != null)
                    {
                        _downloadItems.Add(new DownloadItemViewModel
                        {
                            DisciplineType = $"{entry.DisciplineType} / {entry.FolderName}",
                            FileName = entry.DisplayName,
                            StatusIcon = "✗",
                            StatusColor = System.Windows.Media.Brushes.Red,
                            StatusText = entry.Error
                        });
                        noticeCount++;
                    }
                    else
                    {
                        string label = FileTypeLabel(entry.File);
                        _downloadItems.Add(new DownloadItemViewModel
                        {
                            DisciplineType = $"{entry.DisciplineType} / {entry.FolderName}",
                            FileName = string.IsNullOrEmpty(label)
                                ? entry.DisplayName
                                : $"{entry.DisplayName} [{label}]",
                            StatusIcon = "•",
                            StatusColor = System.Windows.Media.Brushes.Gray,
                            StatusText = IsRevitFallback(entry.File)
                                ? "Waiting... (RVT fallback, no NWC linked)"
                                : "Waiting..."
                        });
                    }
                }

                // Count files that can actually be downloaded
                var downloadableFiles = filesToDownload.Where(f => f.File != null).ToList();

                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = downloadableFiles.Count > 0 ? downloadableFiles.Count : 1;
                ProgressBar.Value = 0;

                int successCount = 0;
                int failCount = 0;
                int fallbackCount = 0;
                int downloadIndex = 0;

                for (int i = 0; i < filesToDownload.Count; i++)
                {
                    var entry = filesToDownload[i];
                    var item = _downloadItems[i];

                    // Notice rows are already marked and have nothing to download.
                    if (entry.Error != null)
                    {
                        continue;
                    }

                    // Presigned URLs expire after an hour - refresh before they go stale.
                    if (DateTime.UtcNow - _listFetchedAtUtc > UrlRefreshAfter)
                    {
                        ProgressText.Text = "Refreshing download links...";
                        await RefreshUrlsAsync(filesToDownload);
                    }

                    bool isFallback = IsRevitFallback(entry.File);

                    downloadIndex++;
                    item.StatusIcon = "↓";
                    item.StatusColor = System.Windows.Media.Brushes.DodgerBlue;
                    item.StatusText = $"Downloading {entry.File.FileName}...";
                    ProgressText.Text = $"Downloading {entry.DisciplineType} / {entry.FolderName} ({downloadIndex}/{downloadableFiles.Count})...";

                    string disciplineFolder = Path.Combine(_downloadPath, entry.DisciplineType, entry.FolderName);
                    string result = await BinaApiService.DownloadFileAsync(
                        entry.File.FileUrl, disciplineFolder, entry.File.FileName);

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Navisworks opens .rvt directly through its Revit file reader, so the
                        // backend's .rvt fallback is a success - noted in the text, not flagged.
                        item.StatusIcon = "✓";
                        item.StatusColor = System.Windows.Media.Brushes.Green;
                        item.StatusText = isFallback
                            ? $"{result} (RVT fallback, no NWC linked)"
                            : result;
                        successCount++;
                        if (isFallback) fallbackCount++;
                    }
                    else
                    {
                        item.StatusIcon = "✗";
                        item.StatusColor = System.Windows.Media.Brushes.Red;
                        item.StatusText = "Download failed";
                        failCount++;
                    }

                    ProgressBar.Value = downloadIndex;
                }

                // Update final status
                if (failCount == 0 && noticeCount == 0 && successCount > 0)
                {
                    HeaderText.Text = "Download Complete";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else if (successCount == 0)
                {
                    HeaderText.Text = "Download Failed";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    HeaderText.Text = "Download Partial";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Orange;
                }

                string statusText = $"Completed: {successCount} successful";
                if (fallbackCount > 0)
                    statusText += $" ({fallbackCount} RVT fallback)";
                if (failCount > 0)
                    statusText += $", {failCount} failed";
                if (noticeCount > 0)
                    statusText += $", {noticeCount} unavailable";
                ProgressText.Text = statusText;
                SummaryText.Text = $"Files saved to: {_downloadPath}";
                CloseButton.IsEnabled = true;
                OpenFolderButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                HeaderText.Text = "Error";
                HeaderText.Foreground = System.Windows.Media.Brushes.Red;
                ProgressText.Text = $"An error occurred: {ex.Message}";
                ProgressBar.IsIndeterminate = false;
                CloseButton.IsEnabled = true;
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Directory.Exists(_downloadPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", _downloadPath);
                }
            }
            catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class DownloadItemViewModel : INotifyPropertyChanged
    {
        private string _statusIcon;
        private string _statusText;
        private System.Windows.Media.Brush _statusColor;

        public string DisciplineType { get; set; }
        public string FileName { get; set; }

        public string StatusIcon
        {
            get => _statusIcon;
            set { _statusIcon = value; OnPropertyChanged(nameof(StatusIcon)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public System.Windows.Media.Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
