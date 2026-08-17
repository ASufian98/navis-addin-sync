using System;
using System.Windows;

namespace NavisWebAppSync
{
    public partial class UserInfoWindow : Window
    {
        private readonly BinaConfig _config;

        public bool LoggedOut { get; private set; }
        public bool SwitchProject { get; private set; }

        public UserInfoWindow(BinaConfig config)
        {
            InitializeComponent();
            _config = config;

            // Display user info
            UserNameText.Text = config.UserName ?? config.Email ?? "Unknown";
            ProjectNameText.Text = config.ProjectName ?? $"Project ID: {config.ProjectId}";
            BimRoleText.Text = config.BimRole ?? "Not assigned";
            DisciplineTypesText.Text = config.DisciplineTypes != null && config.DisciplineTypes.Count > 0
                ? string.Join(", ", config.DisciplineTypes)
                : "All Disciplines";

            // Set environment toggle
            UseStagingCheckbox.IsChecked = config.UseStaging;
            UpdateEnvironmentUrl();
        }

        private void UseStagingCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            bool newValue = UseStagingCheckbox.IsChecked ?? false;
            if (newValue != _config.UseStaging)
            {
                var result = MessageBox.Show(
                    "Changing environment will log you out.\nContinue?",
                    "Environment Change",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _config.UseStaging = newValue;
                    _config.ClearSession();
                    _config.Save();
                    BinaApiService.ClearUrlCache();
                    LoggedOut = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Revert checkbox
                    UseStagingCheckbox.IsChecked = _config.UseStaging;
                }
            }
        }

        private void UpdateEnvironmentUrl()
        {
            EnvironmentUrlText.Text = _config.GetApiBaseUrl();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LoggedOut = true;
                DialogResult = true;
                Close();
            }
        }

        private void SwitchProjectButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchProject = true;
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
