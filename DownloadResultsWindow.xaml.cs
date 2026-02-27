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

        private List<(string DisciplineType, string FolderName, BimLatestFile File, string Error)> GetFilesToDownload(
            BimDisciplineResponse disciplineFiles)
        {
            var filesToDownload = new List<(string DisciplineType, string FolderName, BimLatestFile File, string Error)>();

            void AddDisciplineFiles(string disciplineName, BimDiscipline discipline)
            {
                if (discipline?.Folders == null) return;

                foreach (var folder in discipline.Folders)
                {
                    if (folder == null) continue;

                    // Check if there's an error (no .nwc file linked)
                    if (!string.IsNullOrEmpty(folder.Error))
                    {
                        filesToDownload.Add((disciplineName, folder.Name, null, folder.Error));
                    }
                    else if (folder.LatestFile != null && !string.IsNullOrEmpty(folder.LatestFile.FileUrl))
                    {
                        filesToDownload.Add((disciplineName, folder.Name, folder.LatestFile, null));
                    }
                }
            }

            AddDisciplineFiles("Structure", disciplineFiles.Structure);
            AddDisciplineFiles("Architecture", disciplineFiles.Architecture);
            AddDisciplineFiles("Mechanical", disciplineFiles.Mechanical);
            AddDisciplineFiles("Electrical", disciplineFiles.Electrical);

            return filesToDownload;
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

                var filesToDownload = GetFilesToDownload(disciplineFiles);

                if (filesToDownload.Count == 0)
                {
                    HeaderText.Text = "No Files Available";
                    HeaderText.Foreground = System.Windows.Media.Brushes.Orange;
                    ProgressText.Text = "No discipline files found for this project.";
                    ProgressBar.IsIndeterminate = false;
                    CloseButton.IsEnabled = true;
                    return;
                }

                // Initialize download items
                int errorCount = 0;
                foreach (var (type, folderName, file, error) in filesToDownload)
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        // This folder has no .nwc file linked - show error immediately
                        _downloadItems.Add(new DownloadItemViewModel
                        {
                            DisciplineType = $"{type} / {folderName}",
                            FileName = "(No NWC linked)",
                            StatusIcon = "✗",
                            StatusColor = System.Windows.Media.Brushes.Red,
                            StatusText = error
                        });
                        errorCount++;
                    }
                    else
                    {
                        _downloadItems.Add(new DownloadItemViewModel
                        {
                            DisciplineType = $"{type} / {folderName}",
                            FileName = file.FileName,
                            StatusIcon = "•",
                            StatusColor = System.Windows.Media.Brushes.Gray,
                            StatusText = "Waiting..."
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
                int downloadIndex = 0;

                for (int i = 0; i < filesToDownload.Count; i++)
                {
                    var (type, folderName, file, error) = filesToDownload[i];
                    var item = _downloadItems[i];

                    // Skip items with errors (already marked)
                    if (!string.IsNullOrEmpty(error))
                    {
                        failCount++;
                        continue;
                    }

                    downloadIndex++;
                    item.StatusIcon = "↓";
                    item.StatusColor = System.Windows.Media.Brushes.DodgerBlue;
                    item.StatusText = $"Downloading {file.FileName}...";
                    ProgressText.Text = $"Downloading {type} / {folderName} ({downloadIndex}/{downloadableFiles.Count})...";

                    string disciplineFolder = Path.Combine(_downloadPath, type, folderName);
                    string result = await BinaApiService.DownloadFileAsync(
                        file.FileUrl, disciplineFolder, file.FileName);

                    if (!string.IsNullOrEmpty(result))
                    {
                        item.StatusIcon = "✓";
                        item.StatusColor = System.Windows.Media.Brushes.Green;
                        item.StatusText = result;
                        successCount++;
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
                if (failCount == 0 && successCount > 0)
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
                if (failCount > 0)
                    statusText += $", {failCount - errorCount} failed";
                if (errorCount > 0)
                    statusText += $", {errorCount} missing NWC";
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
