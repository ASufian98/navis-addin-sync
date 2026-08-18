using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    public class BinaConfig
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public int ProjectId { get; set; }
        public int UserId { get; set; }

        // Session data
        public string UserName { get; set; }
        public string ProjectName { get; set; }
        public string BimRole { get; set; }
        public List<string> DisciplineTypes { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime TokenExpiry { get; set; }

        // User preferences
        public string LastDownloadPath { get; set; }

        // Environment setting (production by default, staging for testing)
        public bool UseStaging { get; set; } = false;

        /// <summary>
        /// Returns the API base URL based on environment setting
        /// </summary>
        public string GetApiBaseUrl()
        {
            return UseStaging
                ? "https://api-stg.binacloud.ai"
                : "https://api.binacloud.ai";
        }

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NavisWebAppSync",
            "config.json"
        );

        public static BinaConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonConvert.DeserializeObject<BinaConfig>(json);
                }
            }
            catch (Exception ex)
            {
                // Log config load errors - important for debugging environment issues
                LogError($"Failed to load config: {ex.Message}");
            }

            return new BinaConfig();
        }

        private static void LogError(string message)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "bina_navis_log.txt");
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        public void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception)
            {
            }
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(Password) && ProjectId > 0 && UserId > 0;
        }

        public bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(AccessToken)
                && !string.IsNullOrEmpty(UserName)
                && ProjectId > 0;
        }

        public void ClearSession()
        {
            Email = null;
            Password = null;
            UserName = null;
            ProjectName = null;
            BimRole = null;
            DisciplineTypes = null;
            AccessToken = null;
            RefreshToken = null;
            TokenExpiry = DateTime.MinValue;
            ProjectId = 0;
            UserId = 0;
        }
    }
}
