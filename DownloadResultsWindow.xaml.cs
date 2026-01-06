using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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

                var filesToDownload = new List<(string Type, BimDisciplineFile File)>();
                if (disciplineFiles.Structure != null && !string.IsNullOrEmpty(disciplineFiles.Structure.FileUrl))
                    filesToDownload.Add(("Structure", disciplineFiles.Structure));
                if (disciplineFiles.Architecture != null && !string.IsNullOrEmpty(disciplineFiles.Architecture.FileUrl))
                    filesToDownload.Add(("Architecture", disciplineFiles.Architecture));
                if (disciplineFiles.HVAC != null && !string.IsNullOrEmpty(disciplineFiles.HVAC.FileUrl))
                    filesToDownload.Add(("HVAC", disciplineFiles.HVAC));
                if (disciplineFiles.Electrical != null && !string.IsNullOrEmpty(disciplineFiles.Electrical.FileUrl))
                    filesToDownload.Add(("Electrical", disciplineFiles.Electrical));

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
                foreach (var (type, file) in filesToDownload)
                {
                    _downloadItems.Add(new DownloadItemViewModel
                    {
                        DisciplineType = type,
                        FileName = file.FileName,
                        StatusIcon = "...",
                        StatusText = "Waiting..."
                    });
                }

                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = filesToDownload.Count;
                ProgressBar.Value = 0;

                int successCount = 0;
                int failCount = 0;

                for (int i = 0; i < filesToDownload.Count; i++)
                {
                    var (type, file) = filesToDownload[i];
                    var item = _downloadItems[i];

                    item.StatusIcon = "...";
                    item.StatusText = $"Downloading {file.FileName}...";
                    ProgressText.Text = $"Downloading {type} ({i + 1}/{filesToDownload.Count})...";

                    string disciplineFolder = Path.Combine(_downloadPath, type);
                    string result = await BinaApiService.DownloadFileAsync(
                        file.FileUrl, disciplineFolder, file.FileName);

                    if (!string.IsNullOrEmpty(result))
                    {
                        item.StatusIcon = "[OK]";
                        item.StatusText = result;
                        successCount++;
                    }
                    else
                    {
                        item.StatusIcon = "[X]";
                        item.StatusText = "Download failed";
                        failCount++;
                    }

                    ProgressBar.Value = i + 1;
                }

                // Update final status
                if (failCount == 0)
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

                ProgressText.Text = $"Completed: {successCount} successful, {failCount} failed";
                SummaryText.Text = $"Files saved to: {_downloadPath}";
                CloseButton.IsEnabled = true;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class DownloadItemViewModel : INotifyPropertyChanged
    {
        private string _statusIcon;
        private string _statusText;

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

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
