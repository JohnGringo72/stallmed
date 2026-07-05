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

        public async Task<TResponse> Post<TRequest, TResponse>(string path, TRequest body)
        {
            if (userManager?.User == null)
                return default(TResponse);
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            request.Content = JsonContent.Create(body);
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return default(TResponse);
            }
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }

        public async Task<byte[]> GetBytes(string path)
        {
            if (userManager?.User == null)
                return null;
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<TResponse> PostFile<TResponse>(string path, byte[] fileBytes, string fileName)
        {
            if (userManager?.User == null)
                return default(TResponse);
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(fileBytes);
            content.Add(byteContent, "file", fileName);
            request.Content = content;
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return default(TResponse);
            }
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
    }
}
