using System;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Windows;

namespace NavisWebAppSync
{
    // Event watcher plugin to rename tab at startup
    [Plugin("BINA.EventWatcher", "ACAP", DisplayName = "BINA Event Watcher")]
    public class BinaEventWatcher : EventWatcherPlugin
    {
        private static bool _renamed = false;

        public override void OnLoaded()
        {
            Autodesk.Navisworks.Api.Application.Idle += OnIdle;
        }

        private void OnIdle(object sender, System.EventArgs e)
        {
            if (!_renamed)
            {
                if (RenameToolAddInsTab())
                {
                    _renamed = true;
                    Autodesk.Navisworks.Api.Application.Idle -= OnIdle;
                }
            }
        }

        public override void OnUnloading()
        {
            Autodesk.Navisworks.Api.Application.Idle -= OnIdle;
        }

        private static bool RenameToolAddInsTab()
        {
            try
            {
                var ribbon = ComponentManager.Ribbon;
                if (ribbon != null)
                {
                    foreach (var tab in ribbon.Tabs)
                    {
                        if (tab.Title != null && tab.Title.Contains("Tool add-ins"))
                        {
                            tab.Title = "BINA";
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }
    }

    // Button command - Hello Acap
    [Plugin("BINA.HelloAcap", "ACAP", DisplayName = "Hello Acap", ToolTip = "Say Hello to Acap")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class HelloAcapCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            MessageBox.Show("Hello Acap", "BINA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
    }

    // Button command - Pull Latest Files
    [Plugin("BINA.PullLatestFiles", "ACAP", DisplayName = "Pull Latest Files", ToolTip = "Pull the latest files from BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "..\\..\\Images\\Ribbon_Cloud_16.ico", LargeIcon = "..\\..\\Images\\Ribbon_Cloud_32.ico")]
    public class PullLatestFilesCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            MessageBox.Show("Pull Latest Files - In Progress", "BINA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
    }

    // Button command - Upload Latest Report
    [Plugin("BINA.UploadLatestReport", "ACAP", DisplayName = "Upload Latest Report", ToolTip = "Upload the latest report to BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "..\\..\\Images\\Ribbon_ExportNWD_16.ico", LargeIcon = "..\\..\\Images\\Ribbon_ExportNWD_32.ico")]
    public class UploadLatestReportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            MessageBox.Show("Upload Latest Report - In Progress", "BINA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }
    }

    // Button command - Login To Bina
    [Plugin("BINA.Login", "ACAP", DisplayName = "Login To Bina", ToolTip = "Login to BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "..\\..\\Images\\Ribbon_Cloud_16.ico", LargeIcon = "..\\..\\Images\\Ribbon_Cloud_32.ico")]
    public class LoginCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                var config = BinaConfig.Load();

                if (config.IsLoggedIn())
                {
                    // Show current user info
                    var userInfoWindow = new UserInfoWindow(config);
                    var result = userInfoWindow.ShowDialog();

                    if (result == true)
                    {
                        if (userInfoWindow.LoggedOut)
                        {
                            // Clear session and save
                            config.ClearSession();
                            config.Save();
                            MessageBox.Show("You have been logged out successfully.", "Logged Out", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else if (userInfoWindow.SwitchProject)
                        {
                            // Show project picker with existing token
                            ShowProjectPicker(config);
                        }
                    }
                }
                else
                {
                    // Show login dialog
                    var loginWindow = new LoginWindow(config.Email);
                    var loginResult = loginWindow.ShowDialog();

                    if (loginResult == true)
                    {
                        // Update config with login info
                        config.Email = loginWindow.Email;
                        config.Password = loginWindow.Password;
                        config.AccessToken = loginWindow.AccessToken;
                        config.RefreshToken = loginWindow.RefreshToken;
                        config.TokenExpiry = loginWindow.TokenExpiry;
                        config.UserId = loginWindow.UserId;
                        config.UserName = loginWindow.Email; // Use email as username for now

                        // Show project picker
                        var projectPicker = new ProjectPickerWindow(loginWindow.AccessToken);
                        var projectResult = projectPicker.ShowDialog();

                        if (projectResult == true)
                        {
                            config.ProjectId = projectPicker.SelectedProjectId;
                            config.ProjectName = projectPicker.SelectedProjectName;
                            config.Save();

                            MessageBox.Show(
                                $"Logged in as: {config.UserName}\nProject: {config.ProjectName}",
                                "Login Successful",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                        else
                        {
                            // User cancelled project selection, don't save
                            MessageBox.Show("Login was successful but no project was selected.", "Login Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private void ShowProjectPicker(BinaConfig config)
        {
            var projectPicker = new ProjectPickerWindow(config.AccessToken);
            var result = projectPicker.ShowDialog();

            if (result == true)
            {
                config.ProjectId = projectPicker.SelectedProjectId;
                config.ProjectName = projectPicker.SelectedProjectName;
                config.Save();

                MessageBox.Show(
                    $"Switched to project: {config.ProjectName}",
                    "Project Changed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
