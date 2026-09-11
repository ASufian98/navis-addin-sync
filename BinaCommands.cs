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
            UpdateService.Start();
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

    // Button command - Login To Bina (1st)
    [Plugin("BINA.01_Login", "ACAP", DisplayName = "Login To Bina", ToolTip = "Login to BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "Resources\\login.png", LargeIcon = "Resources\\login.png")]
    public class LoginCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (!UpdateService.EnsureUpToDate()) return 0;

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
                            config.BimRole = projectPicker.SelectedBimRole;
                            config.DisciplineTypes = projectPicker.SelectedDisciplineTypes;
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
                config.BimRole = projectPicker.SelectedBimRole;
                config.DisciplineTypes = projectPicker.SelectedDisciplineTypes;
                config.Save();

                MessageBox.Show(
                    $"Switched to project: {config.ProjectName}",
                    "Project Changed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }

    // Button command - Choose Download Path (2nd)
    [Plugin("BINA.02_ChoosePath", "ACAP", DisplayName = "Choose Path", ToolTip = "Choose the folder path for downloads")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "Resources\\folder.png", LargeIcon = "Resources\\folder.png")]
    public class ChoosePathCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (!UpdateService.EnsureUpToDate()) return 0;

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

    // Button command - Download Model (3rd)
    [Plugin("BINA.03_PullLatestFiles", "ACAP", DisplayName = "Download Model", ToolTip = "Browse and download models from BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "Resources\\download.png", LargeIcon = "Resources\\download.png")]
    public class PullLatestFilesCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (!UpdateService.EnsureUpToDate()) return 0;

            try
            {
                var config = BinaConfig.Load();

                // Check if logged in
                if (!config.IsLoggedIn())
                {
                    MessageBox.Show(
                        "Please login to BINA Cloud first.",
                        "Not Logged In",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 0;
                }

                // Check if project is selected
                if (config.ProjectId <= 0)
                {
                    MessageBox.Show(
                        "Please select a project first (via Login button).",
                        "No Project Selected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 0;
                }

                // Download root: use LastDownloadPath if set (via Choose Path), else Desktop/BINA_Downloads
                // Don't update this from downloaded file path - that causes nesting
                string downloadRoot = config.LastDownloadPath;
                if (string.IsNullOrEmpty(downloadRoot) || !Directory.Exists(downloadRoot))
                {
                    downloadRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "BINA_Downloads");
                }

                // Show model browser
                using (var api = new SyncApiClient(config.GetApiBaseUrl(), config.AccessToken))
                {
                    var browser = new ModelBrowserWindow(api, config.ProjectId, config.ProjectName, downloadRoot);
                    bool? result = browser.ShowDialog();

                    if (result == true && !string.IsNullOrEmpty(browser.DownloadedPath))
                    {
                        // Reveal in explorer
                        try
                        {
                            System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + browser.DownloadedPath + "\"");
                        }
                        catch { }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Download Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }
    }

    // Button command - Upload Latest Report (4th)
    [Plugin("BINA.04_UploadLatestReport", "ACAP", DisplayName = "Upload Clash Report", ToolTip = "Upload clash detection report to BINA Cloud")]
    [AddInPluginAttribute(AddInLocation.AddIn, Icon = "Resources\\upload.png", LargeIcon = "Resources\\upload.png")]
    public class UploadLatestReportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            if (!UpdateService.EnsureUpToDate()) return 0;

            try
            {
                var config = BinaConfig.Load();

                // Check if logged in
                if (!config.IsLoggedIn())
                {
                    MessageBox.Show(
                        "Please login to BINA Cloud first.",
                        "Not Logged In",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 0;
                }

                // Check if project is selected
                if (config.ProjectId <= 0)
                {
                    MessageBox.Show(
                        "Please select a project first.",
                        "No Project Selected",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 0;
                }

                // Show upload window
                var uploadWindow = new ClashUploadWindow(config);
                var result = uploadWindow.ShowDialog();

                if (result == true && uploadWindow.UploadSuccessful)
                {
                    var uploadResult = uploadWindow.UploadResult;
                    string successMessage = $"Clash report uploaded successfully!\n\n" +
                        $"Version: {uploadResult?.Data?.Version ?? 0}\n" +
                        $"Total Clashes: {uploadResult?.Data?.TotalClashes ?? 0}";

                    MessageBox.Show(
                        successMessage,
                        "Upload Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error: {ex.Message}",
                    "Upload Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
