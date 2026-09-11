using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        // Optional config.json overrides (dev use only)
        public string ApiBaseUrlOverride { get; set; }
        public string UpdateFeedUrlOverride { get; set; }

        // ─────────────────────────────────────────────────────────────────────
        // Compile-time environment (embedded .env files)
        // ─────────────────────────────────────────────────────────────────────

        private static readonly Lazy<Dictionary<string, string>> _env =
            new Lazy<Dictionary<string, string>>(LoadEnv);

        /// <summary>
        /// Build environment (compile-time).
        /// </summary>
        public static BuildChannel Channel =>
#if DEBUG
            BuildChannel.Debug;
#elif STAGING
            BuildChannel.Staging;
#else
            BuildChannel.Release;
#endif

        /// <summary>
        /// Human-readable channel description for diagnostics.
        /// </summary>
        public static string ChannelDescription =>
            Channel == BuildChannel.Debug ? "Debug (.env.local)" :
            Channel == BuildChannel.Staging ? "Staging (.env.staging)" :
            "Release (.env.production)";

        private static Dictionary<string, string> LoadEnv()
        {
            // csproj embeds the correct env file as "env" per configuration
            const string resource = "env";
            try
            {
                using (var stream = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    using (var reader = new StreamReader(stream))
                    {
                        return EnvFile.Parse(reader);
                    }
                }
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static string GetEnv(string key, string fallback = "")
        {
            if (_env.Value.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
                return val;
            return fallback;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Resolved URLs (env + optional config.json override)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BINA Cloud API base URL (bina-be).
        /// </summary>
        public string GetApiBaseUrl()
        {
            // Config override for dev tunnels
            if (!string.IsNullOrEmpty(ApiBaseUrlOverride))
                return ApiBaseUrlOverride.TrimEnd('/');

            return GetEnv("API_BASE_URL", "https://api.binacloud.ai").TrimEnd('/');
        }

        /// <summary>
        /// OTA update feed URL (version.json).
        /// </summary>
        public string GetUpdateFeedUrl()
        {
            if (!string.IsNullOrEmpty(UpdateFeedUrlOverride))
                return UpdateFeedUrlOverride;

            return GetEnv("UPDATE_FEED_URL", "");
        }

        /// <summary>
        /// Web origin for login flow.
        /// </summary>
        public string GetCloudWebUrl()
        {
            return GetEnv("CLOUD_WEB_URL", "https://app.binacloud.ai").TrimEnd('/');
        }

        /// <summary>
        /// Diagnostic summary of resolved endpoints.
        /// </summary>
        public string DescribeEndpoints() =>
            "channel    : " + ChannelDescription + "\n" +
            "API base   : " + GetApiBaseUrl() + "\n" +
            "cloud web  : " + GetCloudWebUrl() + "\n" +
            "update feed: " + (string.IsNullOrEmpty(GetUpdateFeedUrl()) ? "(disabled)" : GetUpdateFeedUrl());

        // ─────────────────────────────────────────────────────────────────────
        // Config file persistence
        // ─────────────────────────────────────────────────────────────────────

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

    /// <summary>
    /// Build environment channels.
    /// </summary>
    public enum BuildChannel
    {
        Debug,
        Staging,
        Release
    }
}
