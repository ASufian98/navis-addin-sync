using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    /// <summary>
    /// OTA update service for Navisworks plugin.
    /// Checks version.json feed, downloads and stages updates.
    /// </summary>
    public static class UpdateService
    {
        private const string CompleteMarker = ".complete";

        private static readonly string Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bina", "NavisSync");

        private static readonly string VersionsDir = Path.Combine(Root, "versions");
        private static readonly string StagingDir = Path.Combine(Root, "staging");
        private static readonly string LogPath = Path.Combine(Root, "updater.log");

        private static volatile UpdateFeed _pending;
        private static volatile bool _staged;
        private static bool _checked;

        /// <summary>Newer build waiting. Null = up to date.</summary>
        public static UpdateFeed Pending => _pending;

        /// <summary>True once the pending build is fully staged on disk.</summary>
        public static bool IsStaged => _staged;

        public static Version CurrentVersion => GetCurrentVersion();

        /// <summary>
        /// Check for updates on startup. Non-blocking.
        /// </summary>
        public static void Start()
        {
            if (_checked) return;
            _checked = true;

            var feedUrl = BinaConfig.Load().GetUpdateFeedUrl();
            if (string.IsNullOrWhiteSpace(feedUrl))
            {
                Log("no update feed configured — updater disabled");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await CheckAsync(feedUrl);

                    // Stage silently if update available
                    if (_pending != null && !_pending.Mandatory)
                    {
                        try { await StageAsync(null); }
                        catch (Exception ex) { Log($"silent stage failed: {ex.GetType().Name}"); }
                    }
                }
                catch (Exception ex)
                {
                    Log($"update check failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Command gate. Returns true when running build is usable.
        /// Shows message and returns false when mandatory update is pending.
        /// </summary>
        public static bool EnsureUpToDate()
        {
            var pending = _pending;
            if (pending == null || !pending.Mandatory)
                return true;

            if (_staged)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Update {pending.Version} is downloaded.\n\nPlease close Navisworks and run the installer to apply the update.",
                    "BINA Sync Update",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
                return false;
            }

            // Show update window
            try
            {
                new UpdateWindow().ShowDialog();
            }
            catch (Exception ex)
            {
                Log($"update window failed: {ex}");
            }

            return false;
        }

        /// <summary>
        /// Download + verify + stage the pending build.
        /// </summary>
        public static Task StageAsync(IProgress<(double Fraction, string Status)> progress) =>
            StageCoreAsync(_pending ?? throw new InvalidOperationException("no pending update"), progress);

        private static async Task CheckAsync(string feedUrl)
        {
            using (var http = NewHttp())
            {
                var json = await http.GetStringAsync(feedUrl);
                var feed = JsonConvert.DeserializeObject<UpdateFeed>(json);

                if (feed?.Version == null || feed.Url == null)
                {
                    Log($"malformed feed at {feedUrl}");
                    return;
                }

                if (!Version.TryParse(feed.Version, out var remote))
                {
                    Log($"unparseable feed version '{feed.Version}'");
                    return;
                }

                var current = GetCurrentVersion();
                if (remote <= current)
                {
                    Log($"up to date (current {current}, feed {remote})");
                    return;
                }

                if (File.Exists(Path.Combine(VersionsDir, remote.ToString(), CompleteMarker)))
                {
                    Log($"{remote} already staged");
                    _staged = true;
                }

                Log($"update available: {remote} (current {current}, mandatory {feed.Mandatory})");
                _pending = feed;
            }
        }

        private static async Task StageCoreAsync(UpdateFeed feed, IProgress<(double, string)> progress)
        {
            var remote = Version.Parse(feed.Version);
            var targetDir = Path.Combine(VersionsDir, remote.ToString());

            if (File.Exists(Path.Combine(targetDir, CompleteMarker)))
            {
                _staged = true;
                return;
            }

            Log($"staging {remote} from {feed.Url}");
            Directory.CreateDirectory(StagingDir);
            var zipPath = Path.Combine(StagingDir, $"{remote}.zip");
            var extractDir = Path.Combine(StagingDir, remote.ToString());

            try
            {
                using (var http = NewHttp())
                using (var response = await http.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var total = response.Content.Headers.ContentLength ?? -1L;

                    using (var download = await response.Content.ReadAsStreamAsync())
                    using (var zipStream = File.Create(zipPath))
                    {
                        var buffer = new byte[81920];
                        long done = 0;
                        int read;
                        while ((read = await download.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await zipStream.WriteAsync(buffer, 0, read);
                            done += read;
                            if (total > 0)
                                progress?.Report(((double)done / total * 0.9,
                                    $"Downloading... {done / 1048576.0:F1} / {total / 1048576.0:F1} MB"));
                        }
                    }
                }

                progress?.Report((0.92, "Verifying..."));
                // Fail closed: require SHA-256 hash for all updates
                if (string.IsNullOrWhiteSpace(feed.Sha256))
                    throw new InvalidOperationException("update rejected — feed missing SHA256 hash");

                using (var file = File.OpenRead(zipPath))
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(file);
                    var actual = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    if (!actual.Equals(feed.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("download corrupted (SHA256 mismatch) — try again");
                }

                progress?.Report((0.95, "Installing..."));
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, recursive: true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                File.WriteAllText(Path.Combine(extractDir, CompleteMarker), feed.Version);

                Directory.CreateDirectory(VersionsDir);
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, recursive: true);
                Directory.Move(extractDir, targetDir);

                Log($"staged {remote} → {targetDir}");
                _staged = true;
                progress?.Report((1.0, "Done — restart Navisworks to apply"));
            }
            catch (Exception ex)
            {
                Log($"stage {remote} failed: {ex}");
                throw;
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true); } catch { }
            }
        }

        private static HttpClient NewHttp() =>
            new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        private static Version GetCurrentVersion()
        {
            // Check if running from versions folder
            for (var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                 !string.IsNullOrEmpty(dir);
                 dir = Path.GetDirectoryName(dir))
            {
                if (string.Equals(Path.GetDirectoryName(dir), VersionsDir, StringComparison.OrdinalIgnoreCase)
                    && Version.TryParse(Path.GetFileName(dir), out var fromDir))
                    return fromDir;
            }

            return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 1);
        }

        private static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Root);
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [updater] {message}{Environment.NewLine}",
                    System.Text.Encoding.UTF8);
            }
            catch { }
        }

        public sealed class UpdateFeed
        {
            [JsonProperty("version")] public string Version { get; set; }
            [JsonProperty("url")] public string Url { get; set; }
            [JsonProperty("sha256")] public string Sha256 { get; set; }
            [JsonProperty("notes")] public string Notes { get; set; }
            [JsonProperty("mandatory")] public bool Mandatory { get; set; } = true;
        }
    }
}
