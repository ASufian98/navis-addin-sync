using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    public class BinaApiService
    {
        private static readonly string BaseUrl = "https://58a18544c731.ngrok-free.app";

        /// <summary>
        /// Login with email and password, returns full login response including tokens
        /// </summary>
        public static async Task<LoginResponse> LoginWithCredentialsAsync(string email, string password)
        {
            try
            {
                LogError($"Attempting login to {BaseUrl}/api/auth/user/sign-in with email: {email}");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");
                    httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

                    var loginData = new
                    {
                        email = email,
                        password = password,
                        rememberMe = true
                    };

                    string jsonContent = JsonConvert.SerializeObject(loginData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync($"{BaseUrl}/api/auth/user/sign-in", content);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    LogError($"Login response status: {response.StatusCode}");
                    LogError($"Login response body: {responseBody}");

                    if (!response.IsSuccessStatusCode)
                    {
                        LogError($"Login failed with status: {response.StatusCode}");
                        return null;
                    }

                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseBody);
                    LogError($"Login successful, token received: {!string.IsNullOrEmpty(loginResponse?.AccessToken)}");

                    return loginResponse;
                }
            }
            catch (Exception ex)
            {
                LogError($"Login exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get list of projects available to the user
        /// </summary>
        public static async Task<List<ProjectInfo>> GetUserProjectsAsync(string accessToken)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");
                    httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.GetAsync($"{BaseUrl}/api/cloud-docs/bim-discipline/user/projects");
                    string responseBody = await response.Content.ReadAsStringAsync();

                    LogError($"GetUserProjects response status: {response.StatusCode}");

                    if (!response.IsSuccessStatusCode)
                    {
                        LogError($"GetUserProjects failed: {responseBody}");
                        return null;
                    }

                    var projects = JsonConvert.DeserializeObject<List<ProjectInfo>>(responseBody);
                    return projects;
                }
            }
            catch (Exception ex)
            {
                LogError($"GetUserProjects exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get BIM discipline files for a project
        /// </summary>
        public static async Task<BimDisciplineResponse> GetBimDisciplineFilesAsync(int projectId, string accessToken)
        {
            try
            {
                string url = $"{BaseUrl}/api/cloud-docs/bim-discipline/project/{projectId}/latest-shared-urls";
                LogError($"Fetching BIM discipline files from: {url}");

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");
                    httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.GetAsync(url);
                    string responseBody = await response.Content.ReadAsStringAsync();

                    LogError($"BIM discipline response status: {response.StatusCode}");
                    LogError($"BIM discipline response body: {responseBody}");

                    if (!response.IsSuccessStatusCode)
                    {
                        LogError($"Failed to get BIM discipline files: {response.StatusCode}");
                        return null;
                    }

                    var disciplineResponse = JsonConvert.DeserializeObject<BimDisciplineResponse>(responseBody);

                    // Log what we parsed
                    LogError($"Parsed - Structure: {disciplineResponse?.Structure?.FileUrl ?? "null"}");
                    LogError($"Parsed - Architecture: {disciplineResponse?.Architecture?.FileUrl ?? "null"}");
                    LogError($"Parsed - HVAC: {disciplineResponse?.HVAC?.FileUrl ?? "null"}");
                    LogError($"Parsed - Electrical: {disciplineResponse?.Electrical?.FileUrl ?? "null"}");

                    return disciplineResponse;
                }
            }
            catch (Exception ex)
            {
                LogError($"GetBimDisciplineFiles exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Download a file from URL to specified directory
        /// </summary>
        public static async Task<string> DownloadFileAsync(string fileUrl, string downloadDirectory, string fileName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    var uri = new Uri(fileUrl);
                    fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        fileName = $"discipline_file_{DateTime.Now:yyyyMMdd_HHmmss}.nwd";
                    }
                }

                if (!Directory.Exists(downloadDirectory))
                {
                    Directory.CreateDirectory(downloadDirectory);
                }

                string filePath = Path.Combine(downloadDirectory, fileName);

                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");
                    httpClient.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");

                    var response = await httpClient.GetAsync(fileUrl);
                    response.EnsureSuccessStatusCode();

                    byte[] fileBytes = await response.Content.ReadAsByteArrayAsync();
                    File.WriteAllBytes(filePath, fileBytes);

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                LogError($"Download failed: {ex.Message}");
                return null;
            }
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
    }
}
