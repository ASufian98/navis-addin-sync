using System;
using System.Windows;

namespace NavisWebAppSync
{
    /// <summary>
    /// Blocking update gate. Shown when a mandatory update is available.
    /// </summary>
    public partial class UpdateWindow : Window
    {
        private bool _busy;

        public UpdateWindow()
        {
            InitializeComponent();

            var pending = UpdateService.Pending;
            VersionText.Text = $"BINA Navis Sync {UpdateService.CurrentVersion} → {pending?.Version}";
            NotesText.Text = pending?.Notes ?? "";

            if (UpdateService.IsStaged)
                ShowRestartState();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (UpdateService.IsStaged)
            {
                Close();
                return;
            }

            if (_busy)
                return;
            _busy = true;

            UpdateButton.IsEnabled = false;
            Progress.Visibility = Visibility.Visible;
            Progress.IsIndeterminate = true;
            StatusText.Text = "Starting download...";

            var progress = new Progress<(double Fraction, string Status)>(p =>
            {
                Progress.IsIndeterminate = false;
                Progress.Value = p.Fraction;
                StatusText.Text = p.Status;
            });

            try
            {
                await UpdateService.StageAsync(progress);
                ShowRestartState();
            }
            catch (Exception ex)
            {
                Progress.Visibility = Visibility.Collapsed;
                StatusText.Text = $"Update failed: {ex.Message}";
                UpdateButton.Content = "Try again";
                UpdateButton.IsEnabled = true;
            }
            finally
            {
                _busy = false;
            }
        }

        private void ShowRestartState()
        {
            Progress.Visibility = Visibility.Collapsed;
            StatusText.Text = "Update installed. Restart Navisworks to finish.";
            UpdateButton.Content = "Close — restart Navisworks to apply";
            UpdateButton.IsEnabled = true;
        }
    }
}
