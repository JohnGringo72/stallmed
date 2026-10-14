namespace StallmedManager.Server.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using StallmedManager.Shared.Models;
    using StallmedManager.Server.Models;
    using StallmedManager.Server.Services;

    [ApiController]
    public class AuthController : ControllerBase
    {
        // Ο προεπιλεγμένος κωδικός που δίνει ο admin. Ο χρήστης υποχρεούται να
        // τον αλλάξει στην πρώτη είσοδο, γι' αυτό απαγορεύεται ως νέος κωδικός.
        public const string DefaultPassword = "User1234";
        private const int MinPasswordLength = 6;

        private StallmedContext context;
        private AesService aesService;
        private PasswordHasher hasher;

        public AuthController(StallmedContext context, AesService aesService, PasswordHasher hasher)
        {
            this.context = context;
            this.aesService = aesService;
            this.hasher = hasher;
        }

        [HttpPost]
        [Route("api/auth/login")]
        public async Task<LoginResponse> Login([FromBody] LoginRequest request)
        {
            // Βρίσκουμε τον χρήστη με email, username ή AMKA
            var user = context.Users.SingleOrDefault(u =>
                (u.Email == request.EmailUsernameAMKA ||
                 u.Username == request.EmailUsernameAMKA ||
                 u.AMKA == request.EmailUsernameAMKA)
                && u.Active == true);

            if (user == null)
            {
                return new LoginResponse()
                {
                    Success = false,
                    Message = "Λάθος στοιχεία σύνδεσης ή ανενεργός χρήστης."
                };
            }

            var passwordOk = VerifyPassword(user, request.Password, out var needsRehash);

            if (!passwordOk)
            {
                return new LoginResponse()
                {
                    Success = false,
                    Message = "Λάθος στοιχεία σύνδεσης ή ανενεργός χρήστης."
                };
            }

            // Παλιός κωδικός (AES ή απλό κείμενο) που μόλις επιβεβαιώθηκε: τον
            // ξαναγράφουμε ως hash, ώστε η βάση να καθαρίζει μόνη της με τη χρήση.
            if (needsRehash)
            {
                user.PasswordEncrypted = hasher.Hash(request.Password);
                user.Password = "";
                await context.SaveChangesAsync();
            }

            user.Token = CreateToken(user);

            // Αποσύνδεση πριν τον καθαρισμό: αλλιώς τα μηδενικά που βάζουμε για την
            // απάντηση κάθονται στην παρακολουθούμενη οντότητα και ένα επόμενο
            // SaveChanges θα έσβηνε τον κωδικό από τη βάση.
            context.Entry(user).State = EntityState.Detached;

            // Ο κωδικός δεν φεύγει ποτέ προς τον browser -- ούτε κρυπτογραφημένος.
            // Το αντικείμενο αποθηκεύεται στο localStorage, οπότε ό,τι μπει εδώ μένει εκεί.
            user.Password = "";
            user.PasswordEncrypted = null;

            return new LoginResponse()
            {
                User = user,
                Success = true,
                Message = "Επιτυχής σύνδεση."
            };
        }

        // ---- Τρέχουσα κατάσταση του συνδεδεμένου χρήστη ----
        // Το token ζει έναν χρόνο και η εφαρμογή κρατά τον χρήστη στο localStorage,
        // οπότε μια ανοιχτή συνεδρία δεν μαθαίνει ποτέ τι άλλαξε στη βάση. Η οθόνη
        // ρωτάει εδώ στο ξεκίνημα: υποχρέωση αλλαγής κωδικού, ρόλος, απενεργοποίηση.
        [Microsoft.AspNetCore.Authorization.Authorize]
        [HttpGet]
        [Route("api/auth/me")]
        public async Task<ActionResult<SessionStatusDto>> Me([FromQuery] int userId)
        {
            var user = await context.Users
                .Where(u => u.IdUser == userId)
                .Select(u => new { u.IdUser, u.Active, u.Role, u.ForcePasswordChange, u.TasksVisibility })
                .FirstOrDefaultAsync();

            if (user == null || !user.Active)
                return Ok(new SessionStatusDto { Valid = false });

            return Ok(new SessionStatusDto
            {
                Valid = true,
                IdUser = user.IdUser,
                Role = user.Role,
                ForcePasswordChange = user.ForcePasswordChange,
                TasksVisibility = user.TasksVisibility
            });
        }

        // ---- Αλλαγή κωδικού από τον ίδιο τον χρήστη ----
        // Δεν απαιτεί ρόλο: τη χρειάζεται και ο χρήστης που μπήκε με τον
        // προεπιλεγμένο κωδικό και είναι υποχρεωμένος να τον αλλάξει.
        [HttpPost]
        [Route("api/auth/change-password")]
        public async Task<ActionResult<UserSaveResult>> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var current = (req.CurrentPassword ?? "").Trim();
            var next = (req.NewPassword ?? "").Trim();

            if (next.Length < MinPasswordLength)
                return Ok(new UserSaveResult
                {
                    Success = false,
                    Message = $"Ο νέος κωδικός πρέπει να έχει τουλάχιστον {MinPasswordLength} χαρακτήρες."
                });

            // Αλλιώς η υποχρεωτική αλλαγή γίνεται ένα κλικ χωρίς νόημα.
            if (string.Equals(next, DefaultPassword, StringComparison.Ordinal))
                return Ok(new UserSaveResult
                {
                    Success = false,
                    Message = "Διάλεξε κωδικό διαφορετικό από τον προεπιλεγμένο."
                });

            if (string.Equals(next, current, StringComparison.Ordinal))
                return Ok(new UserSaveResult
                {
                    Success = false,
                    Message = "Ο νέος κωδικός είναι ίδιος με τον τρέχοντα."
                });

            var user = await context.Users.FindAsync(req.IdUser);
            if (user == null || !user.Active)
                return Ok(new UserSaveResult { Success = false, Message = "Ο χρήστης δεν βρέθηκε." });

            // Επιβεβαίωση του τρέχοντος κωδικού -- χωρίς αυτό, ένα κλεμμένο token
            // θα άλλαζε τον κωδικό και θα κλείδωνε έξω τον κάτοχο.
            if (!VerifyPassword(user, current, out _))
                return Ok(new UserSaveResult { Success = false, Message = "Ο τρέχων κωδικός δεν είναι σωστός." });

            user.PasswordEncrypted = hasher.Hash(next);
            user.Password = "";
            user.ForcePasswordChange = false;
            await context.SaveChangesAsync();

            return Ok(new UserSaveResult
            {
                Success = true,
                IdUser = user.IdUser,
                Message = "Ο κωδικός άλλαξε."
            });
        }

        // Δέχεται και τις τρεις μορφές που μπορεί να έχει η βάση:
        // hash (νέο), AES (παλιό), απλό κείμενο (πολύ παλιό). Το needsRehash
        // λέει ότι ο κωδικός επιβεβαιώθηκε αλλά είναι σε ξεπερασμένη μορφή.
        private bool VerifyPassword(User user, string? attempt, out bool needsRehash)
        {
            needsRehash = false;
            var password = attempt ?? "";

            if (PasswordHasher.IsHashed(user.PasswordEncrypted))
                return hasher.Verify(password, user.PasswordEncrypted);

            if (!string.IsNullOrEmpty(user.PasswordEncrypted))
            {
                var ok = aesService.Decrypt(user.PasswordEncrypted) == password;
                needsRehash = ok;
                return ok;
            }

            // Κενός αποθηκευμένος κωδικός δεν πρέπει να ανοίγει λογαριασμό.
            if (string.IsNullOrEmpty(user.Password)) return false;

            var plainOk = user.Password == password;
            needsRehash = plainOk;
            return plainOk;
        }

        private string CreateToken(User user)
        {
            var secretkey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("THIS IS THE SECRET KEY"));
            var credentials = new SigningCredentials(secretkey, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Sub, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, user.Email ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "")
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