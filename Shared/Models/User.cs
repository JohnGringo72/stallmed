using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    public class User
    {
        [Key]
        public int IdUser { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string? PasswordEncrypted { get; set; }
        public string? AMKA { get; set; }
        public bool ForcePasswordChange { get; set; } = false;
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Role { get; set; }
        public int? IdClient { get; set; }
        public bool Active { get; set; }
        // Υπάρχουν στον πίνακα Users -- χρειάζονται για τη δημιουργία χρήστη
        // και για να φαίνεται η τελευταία σύνδεση στη διαχείριση.
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        [NotMapped]
        public string Token { get; set; }
    }
}