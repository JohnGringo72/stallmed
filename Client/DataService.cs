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
            if (!response.IsSuccessStatusCode)
                return default(T);
            try
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch
            {
                return default(T);
            }
        }

        // Όπως το Get, αλλά κρατά και το μήνυμα λάθους του server (π.χ. 503 «ο Traccar
        // δεν απαντά»), ώστε η σελίδα να δείξει τι έφταιξε αντί για κενή λίστα.
        public async Task<(T? Data, string? Error)> GetWithError<T>(string path)
        {
            if (userManager?.User == null)
                return (default, "Δεν είστε συνδεδεμένοι.");
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
                var response = await http.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    await userManager.Logout();
                    return (default, "Η σύνδεση έληξε.");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return (default, "Δεν έχετε πρόσβαση.");
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync();
                    return (default, string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("<")
                        ? $"Σφάλμα {(int)response.StatusCode}." : text.Trim('"'));
                }
                return (await response.Content.ReadFromJsonAsync<T>(), null);
            }
            catch (Exception)
            {
                return (default, "Πρόβλημα δικτύου.");
            }
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
            if (!response.IsSuccessStatusCode)
                return default(TResponse);
            try
            {
                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch
            {
                return default(TResponse);
            }
        }

        public async Task<TResponse> Put<TRequest, TResponse>(string path, TRequest body)
        {
            if (userManager?.User == null)
                return default(TResponse);
            var request = new HttpRequestMessage(HttpMethod.Put, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            request.Content = JsonContent.Create(body);
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return default(TResponse);
            }
            if (!response.IsSuccessStatusCode)
                return default(TResponse);
            try
            {
                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch
            {
                return default(TResponse);
            }
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
            if (!response.IsSuccessStatusCode)
                return null;
            try
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> PostBytes<TRequest>(string path, TRequest body)
        {
            if (userManager?.User == null)
                return null;
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            request.Content = JsonContent.Create(body);
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return null;
            }
            if (!response.IsSuccessStatusCode)
                return null;
            try
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        // Ο τύπος βγαίνει από την κατάληξη: ο browser δεν τον δίνει όταν το
        // αρχείο στέλνεται ως ωμά bytes.
        private static string MimeFor(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName ?? "").ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".heic" => "image/heic",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".csv" => "text/csv",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        public async Task<TResponse> PostFile<TResponse>(string path, byte[] fileBytes, string fileName)
        {
            if (userManager?.User == null)
                return default(TResponse);
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Add("Authorization", "Bearer " + userManager.User.Token);
            var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(fileBytes);
            // Χωρίς αυτό ο server λαμβάνει κενό ContentType και μετά δεν μπορεί
            // να σερβίρει το αρχείο πίσω (τα συνημμένα δεν άνοιγαν).
            byteContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(MimeFor(fileName));
            content.Add(byteContent, "file", fileName);
            request.Content = content;
            var response = await http.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await userManager.Logout();
                return default(TResponse);
            }
            if (!response.IsSuccessStatusCode)
                return default(TResponse);
            try
            {
                return await response.Content.ReadFromJsonAsync<TResponse>();
            }
            catch
            {
                return default(TResponse);
            }
        }
    }
}
