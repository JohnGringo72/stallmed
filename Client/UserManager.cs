using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using StallmedManager.Shared.Models;
using System.Net.Http.Json;

namespace StallmedManager.Client
{
    public class UserManager
    {
        private ILocalStorageService localStorage;
		private HttpClient http;
		private User user;
		private NavigationManager navigationManager;

        public event EventHandler StatusChanged;

        public UserManager(ILocalStorageService localStorage, HttpClient http, NavigationManager navigationManager)
        {
            this.localStorage = localStorage;
			this.http = http;
			this.navigationManager = navigationManager;
		}

		public async Task<bool> Initialize()
		{
            try
			{
                user = await localStorage.GetItemAsync<User>("user");
            } catch (Exception ex)
			{
				user = null;
			}
			if (user == null) navigationManager.NavigateTo("/login");
            if (StatusChanged != null) StatusChanged(this, new EventArgs());
			return true;
		}

		public User User => user;

		public async Task<LoginResponse> Login(LoginRequest loginRequest)
		{
			using (var loginResponse = await http.PostAsJsonAsync("/api/auth/login", loginRequest, CancellationToken.None))
			{
				var response = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
				if (response.Success)
				{
					user = response.User;
					await localStorage.SetItemAsync("user", response.User);
                    if (StatusChanged != null) StatusChanged(this, new EventArgs());
                }
				return response;
			}
		}

		public async Task<LoginResponse> Login(string emailUsernameAMKA, string password)
        {
			var loginRequest = new LoginRequest() { EmailUsernameAMKA = emailUsernameAMKA, Password = password };
			return await Login(loginRequest);
		}

		public bool IsLoggedIn => User != null;

		public bool IsWarehouse => string.Equals(User?.Role?.Trim(), "warehouse", StringComparison.OrdinalIgnoreCase);

		public bool IsAdmin => string.Equals(User?.Role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

		public async Task<bool> Logout()
		{
			await localStorage.SetItemAsync("user", "");
			user = null;
            if (StatusChanged != null) StatusChanged(this, new EventArgs());
            return true;
		}
	}
}