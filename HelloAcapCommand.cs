using System;
using System.IO;
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

    // Button command - Choose Download Path
    [Plugin("BINA.ChoosePath", "ACAP", DisplayName = "Choose Path", ToolTip = "Choose the folder path for downloads")]
    [AddInPluginAttribute(AddInLocation.AddIn)]
    public class ChoosePathCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var config = BinaConfig.Load();
            string selectedPath = ShowFolderPickerDialog(config.LastDownloadPath);

            if (selectedPath != null)
            {
                config.LastDownloadPath = selectedPath;
                config.Save();
                MessageBox.Show($"Download path set to:\n{selectedPath}", "BINA", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 0;
        }

        private string ShowFolderPickerDialog(string defaultPath)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select folder to save downloaded files";
                dialog.ShowNewFolderButton = true;

                // Set initial directory to the default or previously selected path
                if (!string.IsNullOrEmpty(defaultPath) && Directory.Exists(defaultPath))
                {
                    dialog.SelectedPath = defaultPath;
                }
                else if (!string.IsNullOrEmpty(defaultPath))
                {
                    // Try to use parent directory if the exact path doesn't exist
                    string parentDir = Path.GetDirectoryName(defaultPath);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                    {
                        dialog.SelectedPath = parentDir;
                    }
                }

                DialogResult result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return dialog.SelectedPath;
                }

                return null;
            }
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
