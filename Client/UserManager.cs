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
            else await RefreshFromServer();
            if (StatusChanged != null) StatusChanged(this, new EventArgs());
			return true;
		}

		// Το token ζει έναν χρόνο και ο χρήστης μένει στο localStorage: μια ανοιχτή
		// συνεδρία δεν θα μάθαινε ποτέ ότι άλλαξε ο ρόλος της, ότι απενεργοποιήθηκε,
		// ή ότι της ζητείται αλλαγή κωδικού. Γι' αυτό ρωτάμε τον server.
		private async Task RefreshFromServer()
		{
			try
			{
				var request = new HttpRequestMessage(HttpMethod.Get, $"api/auth/me?userId={user.IdUser}");
				request.Headers.Add("Authorization", "Bearer " + user.Token);
				var response = await http.SendAsync(request);
				if (!response.IsSuccessStatusCode) return;

				var status = await response.Content.ReadFromJsonAsync<SessionStatusDto>();
				if (status == null) return;

				if (!status.Valid)
				{
					await Logout();
					navigationManager.NavigateTo("/login");
					return;
				}

				user.ForcePasswordChange = status.ForcePasswordChange;
				user.Role = status.Role ?? user.Role;
				user.TasksVisibility = status.TasksVisibility;
				user.StartPage = status.StartPage;
				await localStorage.SetItemAsync("user", user);
			}
			catch (Exception)
			{
				// Χωρίς δίκτυο κρατάμε ό,τι ξέραμε -- δεν κλειδώνουμε τον χρήστη έξω.
			}
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

		// Μπήκε με κωδικό που του έδωσε ο admin -- πρέπει να τον αλλάξει
		// πριν κάνει οτιδήποτε άλλο.
		public bool MustChangePassword => User?.ForcePasswordChange == true;

		// Μετά την αλλαγή, η υποχρέωση φεύγει και από το localStorage,
		// αλλιώς το παράθυρο ξαναβγαίνει σε κάθε ανανέωση σελίδας.
		public async Task ClearPasswordChangeFlag()
		{
			if (user == null) return;
			user.ForcePasswordChange = false;
			await localStorage.SetItemAsync("user", user);
			if (StatusChanged != null) StatusChanged(this, new EventArgs());
		}

		public bool IsWarehouse => string.Equals(User?.Role?.Trim(), "warehouse", StringComparison.OrdinalIgnoreCase);

		public bool IsAdmin => string.Equals(User?.Role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase);

		public bool IsVendor => string.Equals(User?.Role?.Trim(), "vendor", StringComparison.OrdinalIgnoreCase);

		// Η αρχική σελίδα του χρήστη ("/" = Home).
		public string StartPagePath => StartPages.Normalize(User?.StartPage) ?? "/";

		// Από το κουμπί 🏠: η σελίδα που βρίσκεται γίνεται η αρχική του ("/" = πίσω στο Home).
		public async Task<UserSaveResult?> SetStartPage(string path)
		{
			if (user == null) return null;
			var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/start-page");
			request.Headers.Add("Authorization", "Bearer " + user.Token);
			request.Content = JsonContent.Create(new SetStartPageRequest { IdUser = user.IdUser, StartPage = path });
			try
			{
				var response = await http.SendAsync(request);
				if (!response.IsSuccessStatusCode) return null;
				var result = await response.Content.ReadFromJsonAsync<UserSaveResult>();
				if (result?.Success == true)
				{
					user.StartPage = StartPages.Normalize(path);
					await localStorage.SetItemAsync("user", user);
				}
				return result;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public async Task<bool> Logout()
		{
			await localStorage.SetItemAsync("user", "");
			user = null;
            if (StatusChanged != null) StatusChanged(this, new EventArgs());
            return true;
		}
	}
}