using System.ComponentModel.DataAnnotations;

namespace StallmedManager.Shared.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email/Username/AMKA is required.")]
        public string EmailUsernameAMKA { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}