using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Διαχείριση χρηστών & ρόλων ----
    // Μόνο για admin, με έλεγχο στον server (policy AdminOnly από το Program.cs) --
    // όχι μόνο στην οθόνη. Οι κωδικοί αποθηκεύονται ΜΟΝΟ κρυπτογραφημένοι
    // (PasswordEncrypted) και η στήλη απλού κειμένου καθαρίζεται.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : ControllerBase
    {
        private const int MinPasswordLength = 6;

        private readonly StallmedContext _context;
        private readonly AesService _aes;
        private readonly ILogger<UsersController> _logger;

        public UsersController(StallmedContext context, AesService aes, ILogger<UsersController> logger)
        {
            _context = context;
            _aes = aes;
            _logger = logger;
        }

        // Ο συνδεδεμένος admin, από το JWT (claim email) -- για να μην μπορεί να
        // κλειδώσει τον εαυτό του έξω.
        private async Task<User?> CurrentUser()
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        private static UserAdminDto ToDto(User u) => new()
        {
            IdUser = u.IdUser,
            Username = u.Username,
            Email = u.Email,
            Firstname = u.Firstname,
            Lastname = u.Lastname,
            AMKA = u.AMKA,
            Role = u.Role,
            Active = u.Active,
            HasEncryptedPassword = !string.IsNullOrEmpty(u.PasswordEncrypted),
            LastLogin = u.LastLogin
        };

        [HttpGet]
        public async Task<ActionResult<List<UserAdminDto>>> GetUsers()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.Active)
                .ThenBy(u => u.Lastname).ThenBy(u => u.Firstname)
                .ToListAsync();
            return Ok(users.Select(ToDto).ToList());
        }

        // Οι ρόλοι που υπάρχουν ήδη, για το dropdown (ο admin μπορεί να γράψει και νέο).
        [HttpGet("roles")]
        public async Task<ActionResult<List<string>>> GetRoles()
        {
            var roles = await _context.Users
                .Where(u => u.Role != null && u.Role != "")
                .Select(u => u.Role)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();
            return Ok(roles);
        }

        [HttpPost("save")]
        public async Task<ActionResult<UserSaveResult>> SaveUser([FromBody] SaveUserRequest req)
        {
            var username = (req.Username ?? "").Trim();
            var role = (req.Role ?? "").Trim();
            var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();

            if (string.IsNullOrWhiteSpace(username))
                return Ok(Fail("Το username είναι υποχρεωτικό."));
            if (string.IsNullOrWhiteSpace(role))
                return Ok(Fail("Ο ρόλος είναι υποχρεωτικός."));

            // Το login ψάχνει με email Ή username Ή AMKA, οπότε πρέπει να είναι μοναδικά.
            if (await _context.Users.AnyAsync(u => u.Username == username && u.IdUser != req.IdUser))
                return Ok(Fail($"Υπάρχει ήδη χρήστης με username '{username}'."));
            if (email != null && await _context.Users.AnyAsync(u => u.Email == email && u.IdUser != req.IdUser))
                return Ok(Fail($"Υπάρχει ήδη χρήστης με email '{email}'."));

            var amka = string.IsNullOrWhiteSpace(req.AMKA) ? null : req.AMKA.Trim();
            if (amka != null && await _context.Users.AnyAsync(u => u.AMKA == amka && u.IdUser != req.IdUser))
                return Ok(Fail($"Υπάρχει ήδη χρήστης με ΑΜΚΑ '{amka}'."));

            if (req.IdUser == null)
            {
                if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Trim().Length < MinPasswordLength)
                    return Ok(Fail($"Δώσε αρχικό κωδικό τουλάχιστον {MinPasswordLength} χαρακτήρων."));

                var user = new User
                {
                    Username = username,
                    Email = email ?? "",
                    Firstname = (req.Firstname ?? "").Trim(),
                    Lastname = (req.Lastname ?? "").Trim(),
                    AMKA = amka,
                    Role = role,
                    Active = req.Active,
                    Password = "",                                   // ποτέ απλό κείμενο
                    PasswordEncrypted = _aes.Encrypt(req.Password.Trim()),
                    ForcePasswordChange = false,
                    CreatedAt = DateTime.Now
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Δημιουργήθηκε χρήστης {Username} με ρόλο {Role}", username, role);
                return Ok(new UserSaveResult { Success = true, IdUser = user.IdUser, Message = "Ο χρήστης δημιουργήθηκε." });
            }

            var existing = await _context.Users.FindAsync(req.IdUser.Value);
            if (existing == null) return Ok(Fail("Ο χρήστης δεν βρέθηκε."));

            var me = await CurrentUser();
            if (me != null && me.IdUser == existing.IdUser)
            {
                if (!string.Equals(role, existing.Role, StringComparison.OrdinalIgnoreCase))
                    return Ok(Fail("Δεν μπορείς να αλλάξεις τον δικό σου ρόλο."));
                if (!req.Active)
                    return Ok(Fail("Δεν μπορείς να απενεργοποιήσεις τον εαυτό σου."));
            }

            if (!await WouldKeepAnActiveAdmin(existing, role, req.Active))
                return Ok(Fail("Πρέπει να μείνει τουλάχιστον ένας ενεργός admin."));

            existing.Username = username;
            existing.Email = email ?? "";
            existing.Firstname = (req.Firstname ?? "").Trim();
            existing.Lastname = (req.Lastname ?? "").Trim();
            existing.AMKA = amka;
            existing.Role = role;
            existing.Active = req.Active;
            await _context.SaveChangesAsync();

            return Ok(new UserSaveResult { Success = true, IdUser = existing.IdUser, Message = "Οι αλλαγές αποθηκεύτηκαν." });
        }

        // ---- Ορισμός / επαναφορά κωδικού (μόνο από admin) ----
        [HttpPost("set-password")]
        public async Task<ActionResult<UserSaveResult>> SetPassword([FromBody] SetUserPasswordRequest req)
        {
            var password = (req.Password ?? "").Trim();
            if (password.Length < MinPasswordLength)
                return Ok(Fail($"Ο κωδικός πρέπει να έχει τουλάχιστον {MinPasswordLength} χαρακτήρες."));

            var user = await _context.Users.FindAsync(req.IdUser);
            if (user == null) return Ok(Fail("Ο χρήστης δεν βρέθηκε."));

            user.PasswordEncrypted = _aes.Encrypt(password);
            user.Password = "";   // καθαρίζει τυχόν παλιό κωδικό σε απλό κείμενο
            await _context.SaveChangesAsync();

            _logger.LogInformation("Αλλαγή κωδικού για τον χρήστη {IdUser}", user.IdUser);
            return Ok(new UserSaveResult { Success = true, IdUser = user.IdUser, Message = "Ο κωδικός ενημερώθηκε." });
        }

        // ---- Ενεργοποίηση / απενεργοποίηση (δεν γίνεται διαγραφή: το όνομα του
        //      χρήστη χρειάζεται στο ιστορικό παραγγελιών, issues και todos) ----
        [HttpPost("set-active")]
        public async Task<ActionResult<UserSaveResult>> SetActive([FromBody] SetUserActiveRequest req)
        {
            var user = await _context.Users.FindAsync(req.IdUser);
            if (user == null) return Ok(Fail("Ο χρήστης δεν βρέθηκε."));

            var me = await CurrentUser();
            if (me != null && me.IdUser == user.IdUser && !req.Active)
                return Ok(Fail("Δεν μπορείς να απενεργοποιήσεις τον εαυτό σου."));

            if (!await WouldKeepAnActiveAdmin(user, user.Role, req.Active))
                return Ok(Fail("Πρέπει να μείνει τουλάχιστον ένας ενεργός admin."));

            user.Active = req.Active;
            await _context.SaveChangesAsync();

            return Ok(new UserSaveResult
            {
                Success = true,
                IdUser = user.IdUser,
                Message = req.Active ? "Ο χρήστης ενεργοποιήθηκε." : "Ο χρήστης απενεργοποιήθηκε."
            });
        }

        // Μετά την αλλαγή, μένει τουλάχιστον ένας ενεργός admin;
        private async Task<bool> WouldKeepAnActiveAdmin(User target, string newRole, bool newActive)
        {
            var wasAdmin = string.Equals(target.Role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) && target.Active;
            var staysAdmin = string.Equals(newRole?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) && newActive;
            if (!wasAdmin || staysAdmin) return true;

            var otherAdmins = await _context.Users
                .CountAsync(u => u.IdUser != target.IdUser && u.Active && u.Role.ToLower() == "admin");
            return otherAdmins > 0;
        }

        private static UserSaveResult Fail(string message) => new() { Success = false, Message = message };
    }
}
