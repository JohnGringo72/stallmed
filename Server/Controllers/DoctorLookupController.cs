using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Κοινή αναζήτηση γιατρού για όλη την εφαρμογή (το <DoctorPicker>) ----
    // Ενιαία λίστα Doctors, ελληνικό όνομα όπου υπάρχει. Ανοιχτή σε κάθε συνδεδεμένο
    // χρήστη: δείχνει μόνο όνομα και πόλη.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DoctorLookupController : ControllerBase
    {
        private readonly StallmedContext _context;

        public DoctorLookupController(StallmedContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<SalesDoctorOptionDto>>> Search([FromQuery] string? search)
        {
            if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
                return Ok(new List<SalesDoctorOptionDto>());
            var s = search.Trim();

            var list = await _context.Doctors
                .Where(d => d.IsActive)
                .Where(d => d.FullName.Contains(s) || (d.FullNameGreek != null && d.FullNameGreek.Contains(s)))
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
    }
}
