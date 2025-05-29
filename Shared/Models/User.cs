using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }    
        public string Password { get; set; }
        public string AMKA { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        
        [NotMapped]
        public string Token { get; set; }
    }
}