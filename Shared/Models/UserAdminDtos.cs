namespace StallmedManager.Shared.Models
{
    // ---- Διαχείριση χρηστών (σελίδα /users, μόνο για admin) ----
    // Κανένα DTO δεν μεταφέρει ποτέ κωδικό προς τον client.

    public class UserAdminDto
    {
        public int IdUser { get; set; }
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? AMKA { get; set; }
        public string Role { get; set; } = "";
        public bool Active { get; set; }
        // false = ο κωδικός είναι ακόμη σε απλό κείμενο στη βάση (παλιοί χρήστες)
        public bool HasEncryptedPassword { get; set; }
        public DateTime? LastLogin { get; set; }

        public string FullName => $"{Firstname} {Lastname}".Trim();
    }

    public class SaveUserRequest
    {
        public int? IdUser { get; set; }            // null = νέος χρήστης
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? AMKA { get; set; }
        public string Role { get; set; } = "";
        public bool Active { get; set; } = true;
        public string? Password { get; set; }       // υποχρεωτικός μόνο στη δημιουργία
    }

    public class SetUserPasswordRequest
    {
        public int IdUser { get; set; }
        public string Password { get; set; } = "";
    }

    public class SetUserActiveRequest
    {
        public int IdUser { get; set; }
        public bool Active { get; set; }
    }

    public class UserSaveResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int IdUser { get; set; }
    }
}
