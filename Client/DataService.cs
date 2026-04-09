using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using StallmedManager.Shared.Models;
using System.Net.Http.Json;

namespace StallmedManager.Client
{
    public class DataService
    {
        private UserManager userManager;
        private HttpClient http;

        public DataService(UserManager userManager, HttpClient http)
        {
            this.userManager = userManager;
            this.http = http;
        }

        public async Task<T> Get<T>(string path)
        {
            if (userManager?.User == null)
                return default(T);

            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            var response = await http.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return default(T);
            }

            return await response.Content.ReadFromJsonAsync<T>();
        }
    }
}