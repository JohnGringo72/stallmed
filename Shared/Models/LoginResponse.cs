namespace StallmedManager.Shared.Models
{
    public class LoginResponse
    {
        public string Message { get; set; } = string.Empty;
		public User User { get; set; }
        public bool Success { get; set; } = false;
    }
}