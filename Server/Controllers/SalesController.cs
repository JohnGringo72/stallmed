using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Sales Module: Επισκέψεις πωλητών + Εξοδολόγιο ----
    // Πίνακες: sql/sales_module.sql. Ορατότητα:
    //  * Επισκέψεις: τις βλέπουν ΟΛΟΙ οι χρήστες (για να ξέρει ο ένας πού
    //    πήγε ο άλλος)· διαγραφή μόνο δικών του (ή admin).
    //  * Εξοδολόγιο: ο καθένας μόνο το δικό του, ο admin όλων.
    // Ο ρόλος ελέγχεται στη βάση (Users) -- όχι από ό,τι δηλώσει ο client.
    // Visits: μόνο vendor/admin (policy ανά endpoint). Εξοδολόγιο: κάθε
    // συνδεδεμένος χρήστης, μόνο τα δικά του (ο έλεγχος γίνεται στη βάση).
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly StallmedContext _context;

        public SalesController(StallmedContext context)
        {
            _context = context;
        }

        private async Task<bool> IsAdmin(int userId)
        {
            var role = await _context.Users
                .Where(u => u.IdUser == userId)
                .Select(u => u.Role)
                .FirstOrDefaultAsync();
            return string.Equals(role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<int, string>> UserNames(IEnumerable<int> ids)
        {
            var wanted = ids.Distinct().ToList();
            return await _context.Users
                .Where(u => wanted.Contains(u.IdUser))
                .ToDictionaryAsync(u => u.IdUser, u => (u.Firstname + " " + u.Lastname).Trim());
        }

        // ================= ΕΠΙΣΚΕΨΕΙΣ =================

        [HttpGet("visits")]
        [Authorize(Policy = "VendorOrAdmin")]
        public async Task<ActionResult<List<SalesVisitViewDto>>> GetVisits(
            [FromQuery] DateTime from, [FromQuery] DateTime to,
            [FromQuery] int? userId, [FromQuery] string? search)
        {
            var query = _context.SalesVisits
                .Where(v => v.VisitDate >= from && v.VisitDate <= to);
            if (userId.HasValue)
                query = query.Where(v => v.UserID == userId.Value);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(v => v.DoctorName.Contains(search));

            var visits = await query
                .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.VisitID)
                .Take(2000)
                .ToListAsync();

            var names = await UserNames(visits.Select(v => v.UserID));
            return Ok(visits.Select(v => new SalesVisitViewDto
            {
                VisitID = v.VisitID,
                UserID = v.UserID,
                UserName = names.TryGetValue(v.UserID, out var n) ? n : $"user #{v.UserID}",
                DoctorID = v.DoctorID,
                DoctorName = v.DoctorName,
                VisitDate = v.VisitDate,
                Notes = v.Notes
            }).ToList());
        }

        // Τελευταία επίσκεψη ΟΠΟΙΟΥΔΗΠΟΤΕ πωλητή στον γιατρό -- για το
        // «μην πάνε στους ίδιους»: φαίνεται πριν καταχωρηθεί νέα επίσκεψη.
        [HttpGet("visits/last")]
        [Authorize(Policy = "VendorOrAdmin")]
        public async Task<ActionResult<SalesVisitViewDto?>> GetLastVisit(
            [FromQuery] int? doctorId, [FromQuery] string? doctorName)
        {
            var query = _context.SalesVisits.AsQueryable();
            if (doctorId.HasValue)
                query = query.Where(v => v.DoctorID == doctorId.Value);
            else if (!string.IsNullOrWhiteSpace(doctorName))
                query = query.Where(v => v.DoctorName == doctorName);
            else
                return Ok(null);

            var last = await query
                .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.VisitID)
                .FirstOrDefaultAsync();
            if (last == null) return Ok(null);

            var names = await UserNames(new[] { last.UserID });
            return Ok(new SalesVisitViewDto
            {
                VisitID = last.VisitID,
                UserID = last.UserID,
                UserName = names.TryGetValue(last.UserID, out var n) ? n : $"user #{last.UserID}",
                DoctorID = last.DoctorID,
                DoctorName = last.DoctorName,
                VisitDate = last.VisitDate,
                Notes = last.Notes
            });
        }

        [HttpPost("visits/save")]
        [Authorize(Policy = "VendorOrAdmin")]
        public async Task<ActionResult<SalesSaveResult>> SaveVisit([FromBody] SaveSalesVisitRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.DoctorName))
                return Ok(new SalesSaveResult { Success = false, Message = "Το όνομα γιατρού είναι υποχρεωτικό." });

            SalesVisit visit;
            if (req.VisitID.HasValue)
            {
                visit = await _context.SalesVisits.FindAsync(req.VisitID.Value);
                if (visit == null) return NotFound();
                // Επεξεργασία μόνο δικής του επίσκεψης (ή admin).
                if (visit.UserID != req.UserID && !await IsAdmin(req.UserID))
                    return Ok(new SalesSaveResult { Success = false, Message = "Μόνο ο πωλητής που την κατέγραψε μπορεί να την αλλάξει." });
            }
            else
            {
                visit = new SalesVisit { UserID = req.UserID, CreatedAt = DateTime.Now };
                _context.SalesVisits.Add(visit);
            }

            visit.DoctorID = req.DoctorID;
            visit.DoctorName = req.DoctorName.Trim();
            visit.VisitDate = req.VisitDate.Date;
            visit.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            await _context.SaveChangesAsync();
            return Ok(new SalesSaveResult { Success = true });
        }

        [HttpPost("visits/delete")]
        [Authorize(Policy = "VendorOrAdmin")]
        public async Task<ActionResult<SalesSaveResult>> DeleteVisit([FromBody] DeleteSalesVisitRequest req)
        {
            var visit = await _context.SalesVisits.FindAsync(req.VisitID);
            if (visit == null) return NotFound();
            if (visit.UserID != req.UserID && !await IsAdmin(req.UserID))
                return Ok(new SalesSaveResult { Success = false, Message = "Μόνο ο πωλητής που την κατέγραψε μπορεί να τη σβήσει." });

            _context.SalesVisits.Remove(visit);
            await _context.SaveChangesAsync();
            return Ok(new SalesSaveResult { Success = true });
        }

        // Autocomplete γιατρών για τη φόρμα επίσκεψης: ψάχνει και στο ελληνικό
        // και στο greeklish όνομα, δείχνει το ελληνικό όπου υπάρχει.
        [HttpGet("doctors")]
        [Authorize(Policy = "VendorOrAdmin")]
        public async Task<ActionResult<List<SalesDoctorOptionDto>>> SearchDoctors([FromQuery] string? search)
        {
            var query = _context.Doctors.Where(d => d.IsActive);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => d.FullName.Contains(search)
                    || (d.FullNameGreek != null && d.FullNameGreek.Contains(search)));

            var list = await query
                .OrderBy(d => d.FullNameGreek ?? d.FullName)
                .Take(30)
                .Select(d => new SalesDoctorOptionDto
                {
                    DoctorID = d.DoctorID,
                    DisplayName = d.FullNameGreek ?? d.FullName,
                    City = d.City
                }).ToListAsync();
            return Ok(list);
        }

        // Ενεργοί χρήστες: φίλτρο πωλητή στις επισκέψεις + επιλογή χρήστη
        // στο εξοδολόγιο (admin).
        [HttpGet("users")]
        public async Task<ActionResult<List<IssueUserDto>>> GetUsers()
        {
            var users = await _context.Users
                .Where(u => u.Active)
                .OrderBy(u => u.Lastname).ThenBy(u => u.Firstname)
                .Select(u => new IssueUserDto
                {
                    IdUser = u.IdUser,
                    FullName = (u.Firstname + " " + u.Lastname).Trim(),
                    Role = u.Role
                }).ToListAsync();
            return Ok(users);
        }

        // ================= ΕΞΟΔΟΛΟΓΙΟ =================

        [HttpGet("expenses")]
        public async Task<ActionResult<SalesExpenseMonthDto>> GetExpenses(
            [FromQuery] int actingUserId, [FromQuery] int userId,
            [FromQuery] int year, [FromQuery] int month)
        {
            // Ιδιωτικό: ξένο εξοδολόγιο βλέπει μόνο ο admin.
            if (userId != actingUserId && !await IsAdmin(actingUserId))
                return Forbid();

            var fromDate = new DateTime(year, month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            var rows = await _context.SalesExpenses
                .Where(e => e.UserID == userId && e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                .OrderBy(e => e.ExpenseDate)
                .ToListAsync();

            var info = await _context.SalesExpenseMonths
                .FirstOrDefaultAsync(m => m.UserID == userId && m.Year == year && m.Month == month);

            return Ok(new SalesExpenseMonthDto
            {
                UserID = userId,
                Year = year,
                Month = month,
                VehiclePlate = info?.VehiclePlate,
                Kilometers = info?.Kilometers,
                Days = rows.Select(e => new SalesExpenseDayDto
                {
                    ExpenseDate = e.ExpenseDate,
                    Destination = e.Destination,
                    Fuel = e.Fuel,
                    Tolls = e.Tolls,
                    Lodging = e.Lodging,
                    Meals = e.Meals,
                    Parking = e.Parking,
                    Tickets = e.Tickets,
                    Misc = e.Misc
                }).ToList()
            });
        }

        // Γρήγορη καταχώρηση ενός εξόδου: προστίθεται στη γραμμή της ημέρας.
        [HttpPost("expenses/add")]
        public async Task<ActionResult<SalesSaveResult>> AddExpense([FromBody] AddSalesExpenseRequest req)
        {
            if (req.UserID != req.ActingUserID && !await IsAdmin(req.ActingUserID))
                return Forbid();
            if (req.Amount <= 0)
                return Ok(new SalesSaveResult { Success = false, Message = "Το ποσό πρέπει να είναι μεγαλύτερο από 0." });

            var validCategories = new[] { "Fuel", "Tolls", "Lodging", "Meals", "Parking", "Tickets", "Misc" };
            if (!validCategories.Contains(req.Category))
                return Ok(new SalesSaveResult { Success = false, Message = "Μη έγκυρη κατηγορία εξόδου." });

            var day = req.ExpenseDate.Date;
            var row = await _context.SalesExpenses
                .FirstOrDefaultAsync(e => e.UserID == req.UserID && e.ExpenseDate == day);
            if (row == null)
            {
                row = new SalesExpense { UserID = req.UserID, ExpenseDate = day, CreatedAt = DateTime.Now };
                _context.SalesExpenses.Add(row);
            }

            switch (req.Category)
            {
                case "Fuel": row.Fuel += req.Amount; break;
                case "Tolls": row.Tolls += req.Amount; break;
                case "Lodging": row.Lodging += req.Amount; break;
                case "Meals": row.Meals += req.Amount; break;
                case "Parking": row.Parking += req.Amount; break;
                case "Tickets": row.Tickets += req.Amount; break;
                case "Misc": row.Misc += req.Amount; break;
            }

            if (!string.IsNullOrWhiteSpace(req.Destination))
            {
                var dest = req.Destination.Trim();
                row.Destination = string.IsNullOrWhiteSpace(row.Destination)
                    ? dest
                    : (row.Destination.Contains(dest) ? row.Destination : row.Destination + " / " + dest);
            }
            row.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new SalesSaveResult { Success = true });
        }

        [HttpPost("expenses/save")]
        public async Task<ActionResult<SalesSaveResult>> SaveExpenses([FromBody] SaveSalesExpenseMonthRequest req)
        {
            if (req.UserID != req.ActingUserID && !await IsAdmin(req.ActingUserID))
                return Forbid();
            if (req.Month < 1 || req.Month > 12)
                return Ok(new SalesSaveResult { Success = false, Message = "Μη έγκυρος μήνας." });

            var fromDate = new DateTime(req.Year, req.Month, 1);
            var toDate = fromDate.AddMonths(1).AddDays(-1);

            // Αντικατάσταση όλου του μήνα: απλό και προβλέψιμο -- ό,τι στέλνει
            // ο client (μη κενές γραμμές) είναι η πλήρης εικόνα του μήνα.
            var existing = await _context.SalesExpenses
                .Where(e => e.UserID == req.UserID && e.ExpenseDate >= fromDate && e.ExpenseDate <= toDate)
                .ToListAsync();
            _context.SalesExpenses.RemoveRange(existing);

            foreach (var d in req.Days)
            {
                var isEmpty = string.IsNullOrWhiteSpace(d.Destination)
                    && d.Fuel == 0 && d.Tolls == 0 && d.Lodging == 0 && d.Meals == 0
                    && d.Parking == 0 && d.Tickets == 0 && d.Misc == 0;
                if (isEmpty) continue;
                if (d.ExpenseDate < fromDate || d.ExpenseDate > toDate) continue;

                _context.SalesExpenses.Add(new SalesExpense
                {
                    UserID = req.UserID,
                    ExpenseDate = d.ExpenseDate.Date,
                    Destination = string.IsNullOrWhiteSpace(d.Destination) ? null : d.Destination.Trim(),
                    Fuel = d.Fuel,
                    Tolls = d.Tolls,
                    Lodging = d.Lodging,
                    Meals = d.Meals,
                    Parking = d.Parking,
                    Tickets = d.Tickets,
                    Misc = d.Misc,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            var info = await _context.SalesExpenseMonths
                .FirstOrDefaultAsync(m => m.UserID == req.UserID && m.Year == req.Year && m.Month == req.Month);
            if (info == null)
            {
                info = new SalesExpenseMonth { UserID = req.UserID, Year = req.Year, Month = req.Month };
                _context.SalesExpenseMonths.Add(info);
            }
            info.VehiclePlate = string.IsNullOrWhiteSpace(req.VehiclePlate) ? null : req.VehiclePlate.Trim();
            info.Kilometers = req.Kilometers;

            await _context.SaveChangesAsync();
            return Ok(new SalesSaveResult { Success = true });
        }
    }
}
