using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace NavisWebAppSync
{
    public partial class ClashUploadWindow : Window
    {
        private readonly BinaConfig _config;
        private string _selectedFilePath;

        public bool UploadSuccessful { get; private set; }
        public ClashUploadResponse UploadResult { get; private set; }

        public ClashUploadWindow(BinaConfig config)
        {
            InitializeComponent();
            _config = config;

            // Load categories
            CategoryListBox.ItemsSource = ClashCategoryInfo.GetAllCategories();

            // Enable upload button when both file and category are selected
            CategoryListBox.SelectionChanged += OnSelectionChanged;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUploadButtonState();
        }

        private void UpdateUploadButtonState()
        {
            UploadButton.IsEnabled = !string.IsNullOrEmpty(_selectedFilePath) && CategoryListBox.SelectedItem != null;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Clash Detection HTML Report",
                Filter = "HTML Files (*.html;*.htm)|*.html;*.htm|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _selectedFilePath = openFileDialog.FileName;
                FilePathTextBox.Text = _selectedFilePath;
                UpdateUploadButtonState();
                HideError();
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCategory = CategoryListBox.SelectedItem as ClashCategoryInfo;
            if (selectedCategory == null)
            {
                ShowError("Please select a category.");
                return;
            }

            if (string.IsNullOrEmpty(_selectedFilePath))
            {
                ShowError("Please select a file to upload.");
                return;
            }

            await UploadFileAsync(selectedCategory.Category);
        }

        private async Task UploadFileAsync(ClashCategory category)
        {
            try
            {
                // Show progress, disable buttons
                SetUploadingState(true);
                HideError();

                string reportName = string.IsNullOrWhiteSpace(ReportNameTextBox.Text)
                    ? null
                    : ReportNameTextBox.Text.Trim();

                var result = await Task.Run(() => BinaApiService.UploadClashReportAsync(
                    _config.ProjectId,
                    _config.AccessToken,
                    _selectedFilePath,
                    category,
                    reportName
                ));

                if (result != null && result.Success)
                {
                    UploadSuccessful = true;
                    UploadResult = result;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    string errorMessage = result?.Message ?? "Upload failed. Please try again.";
                    ShowError(errorMessage);
                    SetUploadingState(false);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Upload failed: {ex.Message}");
                SetUploadingState(false);
            }
        }

        private void SetUploadingState(bool uploading)
        {
            ProgressPanel.Visibility = uploading ? Visibility.Visible : Visibility.Collapsed;
            UploadButton.IsEnabled = !uploading;
            CancelButton.IsEnabled = !uploading;
            BrowseButton.IsEnabled = !uploading;
            CategoryListBox.IsEnabled = !uploading;
            ReportNameTextBox.IsEnabled = !uploading;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorMessage.Visibility = Visibility.Collapsed;
        }
    }
}
