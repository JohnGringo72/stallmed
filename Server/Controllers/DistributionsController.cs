using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Διανομές σε γιατρούς (sql/distributions.sql) ----
    // Όλοι οι χρήστες βλέπουν και καταχωρούν (για να μη δοθεί κάτι διπλό).
    // Διαγραφή: μόνο όποιος το καταχώρησε ή το έδωσε, ή admin.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DistributionsController : ControllerBase
    {
        private readonly StallmedContext _context;
        private readonly ILogger<DistributionsController> _logger;

        public DistributionsController(StallmedContext context, ILogger<DistributionsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // item κενό = όλα τα είδη. doctorId = το ιστορικό ενός γιατρού (για την προειδοποίηση «πήρε ήδη»).
        [HttpGet]
        public async Task<ActionResult<List<Distribution>>> GetList([FromQuery] string? item, [FromQuery] int? doctorId)
        {
            try
            {
                var query = _context.Distributions.AsQueryable();
                if (!string.IsNullOrWhiteSpace(item))
                    query = query.Where(d => d.Item == item.Trim());
                if (doctorId.HasValue)
                    query = query.Where(d => d.DoctorID == doctorId.Value);
                return Ok(await query
                    .OrderByDescending(d => d.GivenDate).ThenByDescending(d => d.DistributionID)
                    .Take(5000)
                    .ToListAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία ανάγνωσης διανομών -- τρέξε το sql/distributions.sql");
                return Ok(new List<Distribution>());
            }
        }

        // Τα είδη που έχουν ήδη καταχωρηθεί (για τη λίστα επιλογής της φόρμας/φίλτρου).
        [HttpGet("items")]
        public async Task<ActionResult<List<string>>> GetItems()
        {
            try
            {
                return Ok(await _context.Distributions.Select(d => d.Item).Distinct().OrderBy(i => i).ToListAsync());
            }
            catch (Exception)
            {
                return Ok(new List<string>());
            }
        }

        [HttpPost("save")]
        public async Task<ActionResult<DistributionSaveResult>> Save([FromBody] SaveDistributionRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.DoctorName))
                return Ok(Fail("Διάλεξε γιατρό."));
            if (req.Quantity <= 0)
                return Ok(Fail("Η ποσότητα πρέπει να είναι τουλάχιστον 1."));
            if (string.IsNullOrWhiteSpace(req.Item))
                return Ok(Fail("Γράψε τι δόθηκε."));

            var giverId = req.GivenByUserID > 0 ? req.GivenByUserID : req.UserID;
            var giverName = await _context.Users
                .Where(u => u.IdUser == giverId)
                .Select(u => (u.Firstname + " " + u.Lastname).Trim())
                .FirstOrDefaultAsync();
            if (giverName == null)
                return Ok(Fail("Ο χρήστης που το έδωσε δεν βρέθηκε."));

            _context.Distributions.Add(new Distribution
            {
                Item = req.Item.Trim(),
                DoctorID = req.DoctorID,
                DoctorName = req.DoctorName.Trim(),
                Quantity = req.Quantity,
                GivenDate = req.GivenDate.Date,
                GivenByUserID = giverId,
                GivenByName = giverName,
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                CreatedBy = req.UserID,
                CreatedAt = DateTime.Now
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποθήκευσης διανομής -- τρέξε το sql/distributions.sql");
                return Ok(Fail("Δεν αποθηκεύτηκε. Υπάρχει ο πίνακας Distributions;"));
            }
            return Ok(new DistributionSaveResult { Success = true });
        }

        [HttpPost("delete")]
        public async Task<ActionResult<DistributionSaveResult>> Delete([FromBody] DeleteDistributionRequest req)
        {
            var row = await _context.Distributions.FindAsync(req.DistributionID);
            if (row == null) return Ok(Fail("Η εγγραφή δεν βρέθηκε."));

            var isAdmin = await _context.Users
                .Where(u => u.IdUser == req.UserID)
                .Select(u => u.Role)
                .FirstOrDefaultAsync() is string role
                && string.Equals(role.Trim(), "admin", StringComparison.OrdinalIgnoreCase);
            if (row.CreatedBy != req.UserID && row.GivenByUserID != req.UserID && !isAdmin)
                return Ok(Fail("Μόνο όποιος το καταχώρησε ή το έδωσε μπορεί να το σβήσει."));

            _context.Distributions.Remove(row);
            await _context.SaveChangesAsync();
            return Ok(new DistributionSaveResult { Success = true });
        }

        private static DistributionSaveResult Fail(string message) => new() { Success = false, Message = message };
    }
}
