namespace StallmedManager.Server.Controllers
{
    // AuthController.cs
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
			var user = context.Users.SingleOrDefault(u => (u.Email == request.EmailUsernameAMKA || u.Username == request.EmailUsernameAMKA || u.AMKA == request.EmailUsernameAMKA ) && u.Password == request.Password);
			if (user == null) return new LoginResponse() { Success = false, Message = "Login failed!" };
            user.Token = CreateToken(user);
			return new LoginResponse() { User = user, Success = true };
		}

		private string CreateToken(User user)
        {
            var secretkey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("THIS IS THE SECRET KEY")); // NOTE: SAME KEY AS USED IN Program.cs FILE
            var credentials = new SigningCredentials(secretkey, SecurityAlgorithms.HmacSha256);

            var claims = new[] // NOTE: could also use List<Claim> here
            {
            new Claim(ClaimTypes.Name, user.Email), // NOTE: this will be the "User.Identity.Name" value
			new Claim(JwtRegisteredClaimNames.Sub, user.Email),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, user.Email) // NOTE: this could a unique ID assigned to the user by a database
		};

            var token = new JwtSecurityToken(issuer: "domain.com", audience: "domain.com", claims: claims, expires: DateTime.Now.AddMinutes(60), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
 }}
