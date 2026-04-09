namespace StallmedManager.Server.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using StallmedManager.Shared.Models;
    using StallmedManager.Server.Models;

    [ApiController]
    public class AuthController : ControllerBase
    {
        private StallmedContext context;

        public AuthController(StallmedContext context)
        {
            this.context = context;
        }

        [HttpPost]
        [Route("api/auth/login")]
        public LoginResponse Login([FromBody] LoginRequest request)
        {
            var user = context.Users.SingleOrDefault(u =>
                (u.Email == request.EmailUsernameAMKA || u.Username == request.EmailUsernameAMKA)
                && u.Password == request.Password
                && u.Active == true);

            if (user == null)
            {
                return new LoginResponse()
                {
                    Success = false,
                    Message = "Λάθος στοιχεία σύνδεσης ή ανενεργός χρήστης."
                };
            }

            user.Token = CreateToken(user);

            return new LoginResponse()
            {
                User = user,
                Success = true,
                Message = "Επιτυχής σύνδεση."
            };
        }

        private string CreateToken(User user)
        {
            var secretkey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("THIS IS THE SECRET KEY"));
            var credentials = new SigningCredentials(secretkey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, user.Email ?? "")
            };

            var token = new JwtSecurityToken(
                issuer: "domain.com",
                audience: "domain.com",
                claims: claims,
                expires: DateTime.Now.AddYears(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}