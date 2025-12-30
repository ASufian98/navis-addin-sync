using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NavisWebAppSync
{
    public class BinaApiService
    {
        private static readonly string BaseUrl = "https://7439284735f6.ngrok-free.app";

        /// <summary>
        /// Login with email and password, returns full login response including tokens
        /// </summary>
        public static async Task<LoginResponse> LoginWithCredentialsAsync(string email, string password)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(30);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "NavisBinaSync/1.0");

                    var loginData = new
                    {
                        email = email,
                        password = password,
                        rememberMe = true
                    };

                    string jsonContent = JsonConvert.SerializeObject(loginData);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync($"{BaseUrl}/api/auth/user/sign-in", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseBody);

                    return loginResponse;
                }
            }
            catch (Exception)
            {
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
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.GetAsync($"{BaseUrl}/api/cloud-docs/bim-discipline/user/projects");

                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    string responseBody = await response.Content.ReadAsStringAsync();
                    var projects = JsonConvert.DeserializeObject<List<ProjectInfo>>(responseBody);

                    return projects;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
