using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // ---- Ημερολόγιο (καρτέλα μέσα στο Todo, sql/calendar_events.sql) ----
    // Κάθε χρήστης φτιάχνει γεγονότα μόνο για τον εαυτό του. Γεγονότα που βλέπουν
    // κι άλλοι (όλοι / ομάδα / συγκεκριμένος χρήστης) και η εταιρεία που αφορούν:
    // μόνο admin -- και η εταιρεία φαίνεται μόνο σε admin.
    // Ο ρόλος ελέγχεται στη βάση (Users), όχι από ό,τι δηλώσει ο client.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CalendarController : ControllerBase
    {
        private readonly StallmedContext _context;
        private readonly ILogger<CalendarController> _logger;

        public CalendarController(StallmedContext context, ILogger<CalendarController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<(bool IsAdmin, string? Team)> UserInfo(int userId)
        {
            var u = await _context.Users
                .Where(x => x.IdUser == userId && x.Active)
                .Select(x => new { x.Role, x.Team })
                .FirstOrDefaultAsync();
            return (string.Equals(u?.Role?.Trim(), "admin", StringComparison.OrdinalIgnoreCase), u?.Team);
        }

        // Γεγονότα που βλέπει ο χρήστης και πέφτουν (έστω και εν μέρει) στο διάστημα [from, to].
        [HttpGet]
        public async Task<ActionResult<List<CalendarEventDto>>> GetEvents([FromQuery] int userId,
            [FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (userId <= 0) return Ok(new List<CalendarEventDto>());
            var (isAdmin, team) = await UserInfo(userId);

            try
            {
                var events = await _context.CalendarEvents
                    .Where(e => e.StartDate <= to.Date && (e.EndDate ?? e.StartDate) >= from.Date)
                    .Where(e => e.CreatedBy == userId
                             || e.VisibleTo == CalendarVisibility.All
                             || (e.VisibleTo == CalendarVisibility.Team && team != null && e.VisibleTeam == team)
                             || (e.VisibleTo == CalendarVisibility.User && e.VisibleUserID == userId))
                    .OrderBy(e => e.StartDate)
                    .ThenBy(e => e.Title)
                    .ToListAsync();

                var userIds = events.Where(e => e.VisibleUserID.HasValue).Select(e => e.VisibleUserID!.Value).Distinct().ToList();
                var names = await _context.Users
                    .Where(u => userIds.Contains(u.IdUser))
                    .ToDictionaryAsync(u => u.IdUser, u => (u.Firstname + " " + u.Lastname).Trim());

                return Ok(events.Select(e => new CalendarEventDto
                {
                    EventID = e.EventID,
                    Title = e.Title,
                    Notes = e.Notes,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Company = isAdmin ? e.Company : null,     // οι άλλοι δεν το θέλουν
                    VisibleTo = e.VisibleTo,
                    VisibleTeam = e.VisibleTeam,
                    VisibleUserID = e.VisibleUserID,
                    VisibleLabel = e.VisibleTo switch
                    {
                        CalendarVisibility.All => "Όλοι",
                        CalendarVisibility.Team => Teams.Label(e.VisibleTeam),
                        CalendarVisibility.User => e.VisibleUserID.HasValue && names.TryGetValue(e.VisibleUserID.Value, out var n)
                            ? PersonName.Short(n) : "Χρήστης",
                        _ => "Μόνο εγώ"
                    },
                    CreatedBy = e.CreatedBy,
                    CreatedByName = e.CreatedByName,
                    CanEdit = e.CreatedBy == userId || isAdmin
                }).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία ανάγνωσης ημερολογίου -- τρέξε το sql/calendar_events.sql");
                return Ok(new List<CalendarEventDto>());
            }
        }

        [HttpPost("save")]
        public async Task<ActionResult<CalendarSaveResult>> Save([FromBody] SaveCalendarEventRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Ok(Fail("Ο τίτλος είναι υποχρεωτικός."));
            if (req.EndDate.HasValue && req.EndDate.Value.Date < req.StartDate.Date)
                return Ok(Fail("Η λήξη είναι πριν από την έναρξη."));

            var (isAdmin, _) = await UserInfo(req.UserID);

            CalendarEvent ev;
            if (req.EventID.HasValue)
            {
                ev = await _context.CalendarEvents.FindAsync(req.EventID.Value);
                if (ev == null) return Ok(Fail("Το γεγονός δεν βρέθηκε."));
                if (ev.CreatedBy != req.UserID && !isAdmin) return Ok(Fail("Δεν επιτρέπεται."));
            }
            else
            {
                ev = new CalendarEvent
                {
                    CreatedBy = req.UserID,
                    CreatedByName = req.UserName,
                    CreatedAt = DateTime.Now
                };
                _context.CalendarEvents.Add(ev);
            }

            ev.Title = req.Title.Trim();
            ev.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
            ev.StartDate = req.StartDate.Date;
            ev.EndDate = req.EndDate.HasValue && req.EndDate.Value.Date > req.StartDate.Date ? req.EndDate.Value.Date : null;

            if (isAdmin)
            {
                // Μόνο ο admin μοιράζεται γεγονότα και ορίζει εταιρεία.
                ev.Company = CalendarCompanies.Normalize(req.Company);
                ev.VisibleTo = req.VisibleTo switch
                {
                    CalendarVisibility.All => CalendarVisibility.All,
                    CalendarVisibility.Team when Teams.Normalize(req.VisibleTeam) != null => CalendarVisibility.Team,
                    CalendarVisibility.User when req.VisibleUserID.HasValue => CalendarVisibility.User,
                    _ => CalendarVisibility.Me
                };
                ev.VisibleTeam = ev.VisibleTo == CalendarVisibility.Team ? Teams.Normalize(req.VisibleTeam) : null;
                ev.VisibleUserID = ev.VisibleTo == CalendarVisibility.User ? req.VisibleUserID : null;
            }
            else if (!req.EventID.HasValue)
            {
                ev.VisibleTo = CalendarVisibility.Me;
                ev.Company = null;
            }
            ev.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποθήκευσης γεγονότος -- τρέξε το sql/calendar_events.sql");
                return Ok(Fail("Δεν αποθηκεύτηκε. Υπάρχει ο πίνακας CalendarEvents;"));
            }
            return Ok(new CalendarSaveResult { Success = true, EventID = ev.EventID });
        }

        [HttpPost("delete/{eventId:long}")]
        public async Task<ActionResult<CalendarSaveResult>> Delete(long eventId, [FromQuery] int userId)
        {
            var ev = await _context.CalendarEvents.FindAsync(eventId);
            if (ev == null) return Ok(Fail("Το γεγονός δεν βρέθηκε."));
            var (isAdmin, _) = await UserInfo(userId);
            if (ev.CreatedBy != userId && !isAdmin) return Ok(Fail("Δεν επιτρέπεται."));

            _context.CalendarEvents.Remove(ev);
            await _context.SaveChangesAsync();
            return Ok(new CalendarSaveResult { Success = true, EventID = eventId });
        }

        private static CalendarSaveResult Fail(string message) => new() { Success = false, Message = message };
    }
}
