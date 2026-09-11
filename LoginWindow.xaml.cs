using System;
using System.Threading.Tasks;
using System.Windows;

namespace NavisWebAppSync
{
    public partial class LoginWindow : Window
    {
        public string Email { get; private set; }
        public string Password { get; private set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public DateTime TokenExpiry { get; private set; }
        public int UserId { get; private set; }

        private BinaConfig _config;

        public LoginWindow()
        {
            InitializeComponent();
            _config = BinaConfig.Load();
            // Environment is compile-time; hide the checkbox
            UseStagingCheckbox.Visibility = Visibility.Collapsed;
            UpdateEnvironmentDisplay();
            EmailTextBox.Focus();
        }

        public LoginWindow(string prefillEmail) : this()
        {
            if (!string.IsNullOrEmpty(prefillEmail))
            {
                EmailTextBox.Text = prefillEmail;
                PasswordBox.Focus();
            }
        }

        private void UseStagingCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            // No-op: environment is now compile-time
        }

        private void UpdateEnvironmentDisplay()
        {
            // Environment is compile-time; show channel info
            bool isStaging = BinaConfig.Channel.Contains("Staging");
            bool isDebug = BinaConfig.Channel.Contains("Debug");

            if (isDebug)
            {
                EnvironmentText.Text = "Development";
                EnvironmentText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)); // Blue
            }
            else if (isStaging)
            {
                EnvironmentText.Text = "Staging";
                EnvironmentText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)); // Orange
            }
            else
            {
                EnvironmentText.Text = "Production";
                EnvironmentText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text?.Trim();
            string password = PasswordBox.Password;

            // Validate input
            if (string.IsNullOrEmpty(email))
            {
                ShowError("Please enter your email address.");
                EmailTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your password.");
                PasswordBox.Focus();
                return;
            }

            // Show loading state
            SetLoading(true);
            HideError();

            try
            {
                // Call login API
                var loginResponse = await Task.Run(() => BinaApiService.LoginWithCredentialsAsync(email, password));

                if (loginResponse == null || string.IsNullOrEmpty(loginResponse.AccessToken))
                {
                    ShowError("Login failed. Please check your email and password.");
                    SetLoading(false);
                    return;
                }

                // Store results
                Email = email;
                Password = password;
                AccessToken = loginResponse.AccessToken;
                RefreshToken = loginResponse.RefreshToken;
                UserId = loginResponse.UserId;

                // Convert expiry timestamp to DateTime
                if (loginResponse.AccessTokenExpiry > 0)
                {
                    TokenExpiry = DateTimeOffset.FromUnixTimeMilliseconds(loginResponse.AccessTokenExpiry).DateTime;
                }
                else
                {
                    TokenExpiry = DateTime.Now.AddHours(24); // Default to 24 hours if not provided
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Login error: {ex.Message}");
                SetLoading(false);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ErrorMessage.Text = message;
                ErrorMessage.Visibility = Visibility.Visible;
            });
        }

        private void HideError()
        {
            Dispatcher.Invoke(() =>
            {
                ErrorMessage.Visibility = Visibility.Collapsed;
            });
        }

        private void SetLoading(bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                LoadingPanel.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                LoginButton.IsEnabled = !isLoading;
                CancelButton.IsEnabled = !isLoading;
                EmailTextBox.IsEnabled = !isLoading;
                PasswordBox.IsEnabled = !isLoading;
            });
        }
    }
}
