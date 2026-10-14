using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    // Σύνοψη ημέρας: ποιος έκανε τι. Τη βλέπουν όλοι οι συνδεδεμένοι χρήστες --
    // η δουλειά της ημέρας είναι κοινή εικόνα, όχι έλεγχος.
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DailySummaryController : ControllerBase
    {
        private readonly StallmedContext _context;

        public DailySummaryController(StallmedContext context)
        {
            _context = context;
        }

        // Μία ενέργεια πριν ομαδοποιηθεί: ποιος, πότε, τι.
        private sealed record Action(int? UserId, DateTime At, string Kind,
                                     string Label, string Icon, string Title, string? Detail);

        [HttpGet]
        public async Task<ActionResult<DailySummaryDto>> GetDailySummary([FromQuery] DateTime? date)
        {
            var day = (date ?? DateTime.Today).Date;
            var next = day.AddDays(1);

            var actions = new List<Action>();

            // ---- Νέοι γιατροί / πελάτες ----
            actions.AddRange((await _context.Doctors
                .Where(d => d.CreatedAt >= day && d.CreatedAt < next)
                .Select(d => new { d.CreatedBy, d.CreatedAt, d.FullName, d.FullNameGreek, d.City })
                .AsNoTracking().ToListAsync())
                .Select(d => new Action(d.CreatedBy, d.CreatedAt, "doctor", "Νέοι γιατροί", "🩺",
                    string.IsNullOrWhiteSpace(d.FullNameGreek) ? d.FullName : d.FullNameGreek!, d.City)));

            // ---- Νέες παραγγελίες γιατρών ----
            actions.AddRange((await _context.DoctorOrders
                .Where(o => o.CreatedAt >= day && o.CreatedAt < next)
                .Select(o => new { o.CreatedBy, o.CreatedAt, o.OrderCode, o.DoctorName, o.Company })
                .AsNoTracking().ToListAsync())
                .Select(o => new Action(o.CreatedBy, o.CreatedAt, "order", "Νέες παραγγελίες", "📝",
                    o.OrderCode, $"{o.Company} -- {o.DoctorName}")));

            // ---- Ετοιμάστηκαν (μπήκαν σε κουτί) ----
            actions.AddRange((await _context.DoctorOrders
                .Where(o => o.PreparedAt != null && o.PreparedAt >= day && o.PreparedAt < next)
                .Select(o => new { o.PreparedBy, o.PreparedAt, o.OrderCode, o.DoctorName })
                .AsNoTracking().ToListAsync())
                .Select(o => new Action(o.PreparedBy, o.PreparedAt!.Value, "prepared", "Ετοιμάστηκαν", "📦",
                    o.OrderCode, o.DoctorName)));

            // ---- Παραλαβές prick ----
            actions.AddRange((await _context.StockReceipts
                .Where(r => r.CreatedAt >= day && r.CreatedAt < next)
                .Select(r => new { r.CreatedBy, r.CreatedAt, r.CodePrick, r.ProductTypeCode, r.QuantityReceived })
                .AsNoTracking().ToListAsync())
                .Select(r => new Action(r.CreatedBy, r.CreatedAt, "receipt", "Παραλαβές", "📥",
                    $"{r.CodePrick} ({r.ProductTypeCode})", $"{r.QuantityReceived} τεμ.")));

            // ---- Δεσμεύσεις stock ----
            actions.AddRange((await (
                from a in _context.OrderAllocations
                where a.AllocationDate >= day && a.AllocationDate < next
                join l in _context.DoctorOrderLines on a.OrderLineID equals l.OrderLineID
                join o in _context.DoctorOrders on l.OrderID equals o.OrderID
                select new { a.CreatedBy, a.AllocationDate, a.QuantityAllocated, l.CodePrick, o.OrderCode })
                .AsNoTracking().ToListAsync())
                .Select(a => new Action(a.CreatedBy, a.AllocationDate, "alloc", "Δεσμεύσεις stock", "🔒",
                    $"{a.CodePrick} -> {a.OrderCode}", $"{a.QuantityAllocated} τεμ.")));

            // ---- Αναιρέσεις δέσμευσης ----
            actions.AddRange((await (
                from a in _context.OrderAllocations
                where a.ReversedAt != null && a.ReversedAt >= day && a.ReversedAt < next
                join l in _context.DoctorOrderLines on a.OrderLineID equals l.OrderLineID
                join o in _context.DoctorOrders on l.OrderID equals o.OrderID
                select new { a.ReversedBy, a.ReversedAt, a.QuantityAllocated, l.CodePrick, o.OrderCode, a.ReverseReason })
                .AsNoTracking().ToListAsync())
                .Select(a => new Action(a.ReversedBy, a.ReversedAt!.Value, "unalloc", "Αναιρέσεις δέσμευσης", "🔓",
                    $"{a.CodePrick} <- {a.OrderCode}", a.ReverseReason)));

            // ---- Παραγγελίες παραγωγής ----
            actions.AddRange((await _context.ProductionOrders
                .Where(p => p.CreatedAt >= day && p.CreatedAt < next)
                .Select(p => new { p.CreatedBy, p.CreatedAt, p.ProductionOrderCode, p.Company })
                .AsNoTracking().ToListAsync())
                .Select(p => new Action(p.CreatedBy, p.CreatedAt, "production", "Παραγγελίες παραγωγής", "🏭",
                    p.ProductionOrderCode ?? "", p.Company)));

            // ---- Προσφορές ----
            actions.AddRange((await _context.Quotes
                .Where(q => q.CreatedAt >= day && q.CreatedAt < next)
                .Select(q => new { q.CreatedBy, q.CreatedAt, q.QuoteNumber, q.CustomerName })
                .AsNoTracking().ToListAsync())
                .Select(q => new Action(q.CreatedBy, q.CreatedAt, "quote", "Προσφορές", "💶",
                    q.QuoteNumber, q.CustomerName)));

            // ---- Θέματα / Tasks ----
            actions.AddRange((await _context.IssueTasks
                .Where(i => i.CreatedAt >= day && i.CreatedAt < next)
                .Select(i => new { i.CreatedBy, i.CreatedAt, i.IssueCode, i.Title })
                .AsNoTracking().ToListAsync())
                .Select(i => new Action(i.CreatedBy, i.CreatedAt, "issue", "Νέα θέματα", "🗒️",
                    i.IssueCode, i.Title)));

            actions.AddRange((await (
                from c in _context.IssueComments
                where c.CreatedAt >= day && c.CreatedAt < next
                join i in _context.IssueTasks on c.IssueID equals i.IssueID
                select new { c.UserID, c.CreatedAt, i.IssueCode, c.CommentText })
                .AsNoTracking().ToListAsync())
                .Select(c => new Action(c.UserID, c.CreatedAt, "comment", "Σχόλια σε θέματα", "💬",
                    c.IssueCode, Shorten(c.CommentText))));

            // ---- Επισκέψεις πωλητών ----
            actions.AddRange((await _context.SalesVisits
                .Where(v => v.VisitDate >= day && v.VisitDate < next)
                .Select(v => new { v.UserID, v.VisitDate, v.DoctorName, v.Notes })
                .AsNoTracking().ToListAsync())
                .Select(v => new Action(v.UserID, v.VisitDate, "visit", "Επισκέψεις", "🚗",
                    v.DoctorName, Shorten(v.Notes))));

            // ---- Ονόματα χρηστών ----
            var userIds = actions.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();
            var names = await _context.Users
                .Where(u => userIds.Contains(u.IdUser))
                .Select(u => new { u.IdUser, u.Firstname, u.Lastname, u.Username })
                .AsNoTracking()
                .ToDictionaryAsync(u => u.IdUser, u =>
                {
                    var full = $"{u.Firstname} {u.Lastname}".Trim();
                    return full.Length > 0 ? full : u.Username;
                });

            var users = actions
                .GroupBy(a => a.UserId)
                .Select(g => new DailyUserSummaryDto
                {
                    UserId = g.Key,
                    UserName = g.Key.HasValue && names.TryGetValue(g.Key.Value, out var n)
                        ? n
                        : "Χωρίς χρήστη",
                    TotalActions = g.Count(),
                    Groups = BuildGroups(g)
                })
                .OrderByDescending(u => u.TotalActions)
                .ThenBy(u => u.UserName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // ---- Αποστολές: δεν καταγράφεται ποιος τις έκανε ----
            var shipped = (await _context.DoctorOrders
                .Where(o => o.ShippedAt != null && o.ShippedAt >= day && o.ShippedAt < next)
                .Select(o => new { o.ShippedAt, o.OrderCode, o.DoctorName, o.ShippingCarrier })
                .AsNoTracking().ToListAsync())
                .Select(o => new Action(null, o.ShippedAt!.Value, "shipped", "Αποστολές", "🚚",
                    o.OrderCode, string.IsNullOrWhiteSpace(o.ShippingCarrier)
                        ? "διορθωτικό κλείσιμο"
                        : o.ShippingCarrier))
                .ToList();

            return Ok(new DailySummaryDto
            {
                Date = day,
                Totals = await BuildTotals(day, next),
                Users = users,
                Unattributed = shipped.Count == 0 ? new() : BuildGroups(shipped)
            });
        }

        // Ο "ασθενής" της αποθήκης, σε όλες τις γραφές -- δεν είναι πραγματικός ασθενής.
        private static readonly string[] WarehousePatients = { "AA", "ΑΑ", "AΑ", "ΑA" };

        // ---- Σύνολα ημέρας: μετρήσεις που δεν ανήκουν σε χρήστη ----
        private async Task<List<DailyTotalDto>> BuildTotals(DateTime day, DateTime next)
        {
            var totals = new List<DailyTotalDto>();

            // Παραγγελίες μειγμάτων (παλιό σύστημα). Εξαιρούνται οι ακυρωμένες
            // και οι γραμμές αποθήκης, που δεν είναι παραγγελίες ασθενών.
            var mixOrders = await _context.WebOrders
                .Where(x => x.Ordered >= day && x.Ordered < next &&
                            x.Status != "5" &&
                            (x.Patient == null || !WarehousePatients.Contains(x.Patient.Replace(" ", ""))))
                .Select(x => new { x.QNT, x.Patient })
                .AsNoTracking()
                .ToListAsync();

            totals.Add(new DailyTotalDto
            {
                Kind = "mixorders", Icon = "💉", Label = "Παραγγελίες μειγμάτων",
                Value = mixOrders.Count,
                Detail = $"{mixOrders.Sum(x => x.QNT ?? 0)} τεμάχια"
            });

            // Νέοι ασθενείς: όσοι παρήγγειλαν σήμερα και δεν είχαν ξαναπαραγγείλει ποτέ.
            var todayPatients = mixOrders
                .Select(x => x.Patient)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim())
                .Distinct()
                .ToList();

            var newPatients = 0;
            if (todayPatients.Count > 0)
            {
                var seenBefore = await _context.WebOrders
                    .Where(x => x.Ordered < day && x.Patient != null && todayPatients.Contains(x.Patient))
                    .Select(x => x.Patient!)
                    .Distinct()
                    .AsNoTracking()
                    .ToListAsync();

                newPatients = todayPatients
                    .Except(seenBefore.Select(p => p.Trim()), StringComparer.CurrentCultureIgnoreCase)
                    .Count();
            }

            totals.Add(new DailyTotalDto
            {
                Kind = "newpatients", Icon = "🧑", Label = "Νέοι ασθενείς",
                Value = newPatients,
                Detail = $"από {todayPatients.Count} που παρήγγειλαν"
            });

            // Παραγγελίες prick της ημέρας, με τα τεμάχια που ζητήθηκαν.
            var prickOrderIds = await _context.DoctorOrders
                .Where(o => o.CreatedAt >= day && o.CreatedAt < next)
                .Select(o => o.OrderID)
                .ToListAsync();

            var prickPieces = prickOrderIds.Count == 0 ? 0 : await _context.DoctorOrderLines
                .Where(l => prickOrderIds.Contains(l.OrderID))
                .SumAsync(l => (int?)(l.QuantityRequested - l.QuantityCancelled)) ?? 0;

            totals.Add(new DailyTotalDto
            {
                Kind = "prickorders", Icon = "📝", Label = "Παραγγελίες prick",
                Value = prickOrderIds.Count,
                Detail = $"{prickPieces} τεμάχια"
            });

            // Παραλαβές prick της ημέρας.
            var receipts = await _context.StockReceipts
                .Where(r => r.CreatedAt >= day && r.CreatedAt < next)
                .Select(r => r.QuantityReceived)
                .ToListAsync();

            totals.Add(new DailyTotalDto
            {
                Kind = "receipts", Icon = "📥", Label = "Παραλαβές prick",
                Value = receipts.Count,
                Detail = $"{receipts.Sum()} τεμάχια"
            });

            // Νέοι γιατροί/πελάτες.
            totals.Add(new DailyTotalDto
            {
                Kind = "doctors", Icon = "🩺", Label = "Νέοι γιατροί",
                Value = await _context.Doctors.CountAsync(d => d.CreatedAt >= day && d.CreatedAt < next)
            });

            return totals;
        }

        private static List<DailyActionGroupDto> BuildGroups(IEnumerable<Action> actions) =>
            actions
                .GroupBy(a => new { a.Kind, a.Label, a.Icon })
                .Select(g => new DailyActionGroupDto
                {
                    Kind = g.Key.Kind,
                    Label = g.Key.Label,
                    Icon = g.Key.Icon,
                    Count = g.Count(),
                    Items = g.OrderBy(a => a.At)
                             .Select(a => new DailyActionItemDto { At = a.At, Title = a.Title, Detail = a.Detail })
                             .ToList()
                })
                .OrderByDescending(g => g.Count)
                .ThenBy(g => g.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

        // Τα σχόλια και οι σημειώσεις μπορεί να είναι σεντόνια -- στη σύνοψη
        // θέλουμε μόνο μια γεύση.
        private static string? Shorten(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var flat = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            return flat.Length <= 80 ? flat : flat[..77] + "...";
        }
    }
}
