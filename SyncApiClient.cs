using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    /// <summary>
    /// Client for browsing and downloading from bina-be.
    /// </summary>
    public sealed class SyncApiClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly Func<Task<string>> _refreshToken;

        public SyncApiClient(
            string baseUrl,
            string accessToken,
            HttpClient http = null,
            Func<Task<string>> refreshToken = null)
        {
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
            _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            _http.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");
            _http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
            _refreshToken = refreshToken;
        }

        private void UseToken(string accessToken)
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
        }

        private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> send)
        {
            var resp = await send().ConfigureAwait(false);
            if (resp.StatusCode != HttpStatusCode.Unauthorized || _refreshToken == null) return resp;

            resp.Dispose();
            string fresh = await _refreshToken().ConfigureAwait(false);
            if (string.IsNullOrEmpty(fresh))
                throw new InvalidOperationException(
                    "Your session has expired. Please login again.");

            UseToken(fresh);
            return await send().ConfigureAwait(false);
        }

        /// <summary>
        /// Folders for one area of a project.
        /// </summary>
        public async Task<List<BimFolder>> GetFoldersAsync(
            int projectId, string area = BimArea.Wip, string disciplineType = null)
        {
            string url = $"{_baseUrl}/api/cloud-docs/bim-discipline/project/{projectId}/folders"
                       + $"?area={Uri.EscapeDataString(area ?? BimArea.Wip)}";
            if (!string.IsNullOrEmpty(disciplineType))
                url += $"&disciplineType={Uri.EscapeDataString(disciplineType)}";

            using (var resp = await SendWithRefreshAsync(() => _http.GetAsync(url)).ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Forbidden)
                    throw new BinaAccessDeniedException(
                        "You do not have access to this project's " + BimArea.Label(area) + " folders.");

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Could not load folders (HTTP {(int)resp.StatusCode}): {body}");
                return JsonConvert.DeserializeObject<List<BimFolder>>(body) ?? new List<BimFolder>();
            }
        }

        /// <summary>
        /// Models inside one folder.
        /// </summary>
        public async Task<BimDesignsResponse> GetDesignsAsync(
            int projectId, int folderId, string area = null,
            string search = null, string cursor = null,
            int? limit = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string url = $"{_baseUrl}/api/cloud-docs/bim-discipline/project/{projectId}"
                       + $"/folder/{folderId}/designs";

            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(area)) query.Add("area=" + Uri.EscapeDataString(area));
            if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search));
            if (!string.IsNullOrWhiteSpace(cursor)) query.Add("cursor=" + Uri.EscapeDataString(cursor));
            if (limit.HasValue) query.Add("limit=" + limit.Value);
            if (query.Count > 0) url += "?" + string.Join("&", query);

            using (var resp = await SendWithRefreshAsync(() => _http.GetAsync(url, cancellationToken))
                .ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Forbidden)
                    throw new BinaAccessDeniedException(
                        "You do not have access to the models in this folder.");

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new BimDesignsResponse { Designs = new List<BimDesign>(), Area = area };

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Could not load models (HTTP {(int)resp.StatusCode}): {body}");

                var parsed = JsonConvert.DeserializeObject<BimDesignsResponse>(body)
                             ?? new BimDesignsResponse();
                if (parsed.Designs == null) parsed.Designs = new List<BimDesign>();
                return parsed;
            }
        }

        /// <summary>
        /// Versions of a model by design id.
        /// </summary>
        public async Task<List<DesignVersion>> GetVersionsByDesignAsync(
            int projectId, int designId, string area = null)
        {
            if (designId <= 0)
                throw new ArgumentException("designId is required", nameof(designId));

            string url = $"{_baseUrl}/api/cloud-docs/bim-discipline/sync/versions"
                + $"?projectId={projectId}&designId={designId}";
            if (!string.IsNullOrWhiteSpace(area))
                url += "&area=" + Uri.EscapeDataString(area);

            using (var resp = await SendWithRefreshAsync(() => _http.GetAsync(url)).ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (resp.StatusCode == HttpStatusCode.Forbidden)
                    throw new BinaAccessDeniedException(
                        "You do not have access to this model's version history.");

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    return new List<DesignVersion>();

                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Could not load versions (HTTP {(int)resp.StatusCode}): {body}");

                return JsonConvert.DeserializeObject<DesignVersionsResponse>(body)?.Versions
                       ?? new List<DesignVersion>();
            }
        }

        /// <summary>
        /// Downloads a design to destinationPath with progress reporting.
        /// </summary>
        public async Task DownloadAsync(
            int designId,
            string destinationPath,
            IProgress<(double Fraction, string Message)> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string url = $"{_baseUrl}/api/cloud-docs/bim-discipline/discipline/{designId}/download";

            try
            {
                using (var resp = await SendWithRefreshAsync(() =>
                    _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                    .ConfigureAwait(false))
                {
                    if (resp.StatusCode == HttpStatusCode.Forbidden)
                        throw new BinaAccessDeniedException(
                            "You do not have permission to download this version.");

                    if (!resp.IsSuccessStatusCode)
                    {
                        string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"Could not download version (HTTP {(int)resp.StatusCode}): {body}");
                    }

                    long total = resp.Content.Headers.ContentLength ?? -1L;

                    // Ensure directory exists
                    string dir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    using (var source = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var file = File.Create(destinationPath))
                    {
                        var buffer = new byte[81920];
                        long done = 0;
                        int read;

                        while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                                   .ConfigureAwait(false)) > 0)
                        {
                            await file.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            done += read;

                            if (total > 0)
                                progress?.Report(((double)done / total,
                                    $"Downloading... {done / 1048576.0:F1} / {total / 1048576.0:F1} MB"));
                            else
                                progress?.Report((0.0, $"Downloading... {done / 1048576.0:F1} MB"));
                        }
                    }
                }
            }
            catch
            {
                try { if (File.Exists(destinationPath)) File.Delete(destinationPath); } catch { }
                throw;
            }
        }

        public void Dispose() => _http?.Dispose();
    }
}
