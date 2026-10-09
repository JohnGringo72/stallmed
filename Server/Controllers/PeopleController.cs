using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PeopleController : ControllerBase
    {
        private StallmedContext context;
        private readonly ILogger<PeopleController> _logger;

        public PeopleController(ILogger<PeopleController> logger, StallmedContext context)
        {
            _logger = logger;
            this.context = context;
        }

        [HttpGet]
        [Authorize(Policy = "NotWarehouse")]
        public IEnumerable<WebOrder> Get(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? filter,
            [FromQuery] string? doctor,
            [FromQuery] string? patient,
            [FromQuery] string? pharmacy,
            [FromQuery] string? status)
        {
            var query = context.WebOrders.Where(c =>
                c.Ordered >= fromDate &&
                c.Ordered <= toDate);

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(c =>
                    (c.Patient != null && c.Patient.Contains(filter)) ||
                    (c.Doctor != null && c.Doctor.Contains(filter)) ||
                    (c.Pharmacy != null && c.Pharmacy.Contains(filter)) ||
                    (c.Ref != null && c.Ref.Contains(filter)));
            }
            if (!string.IsNullOrWhiteSpace(doctor))
                query = query.Where(c => c.Doctor == doctor);
            if (!string.IsNullOrWhiteSpace(patient))
                query = query.Where(c => c.Patient == patient);
            if (!string.IsNullOrWhiteSpace(pharmacy))
                query = query.Where(c => c.Pharmacy == pharmacy);
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(c => c.Status == status);

            return query.OrderByDescending(c => c.Ordered).ToList();
        }

        [HttpGet("filter-options")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<OrderFilterOptions> GetFilterOptions()
        {
            var options = new OrderFilterOptions
            {
                Doctors = context.WebOrders
                    .Where(x => x.Doctor != null && x.Doctor != "")
                    .Select(x => x.Doctor).Distinct().OrderBy(x => x).ToList(),

                Patients = context.WebOrders
                    .Where(x => x.Patient != null && x.Patient != "")
                    .Select(x => x.Patient).Distinct().OrderBy(x => x).ToList(),

                Pharmacies = context.WebOrders
                    .Where(x => x.Pharmacy != null && x.Pharmacy != "")
                    .Select(x => x.Pharmacy).Distinct().OrderBy(x => x).ToList(),

                Statuses = context.WebOrders
                    .Where(x => x.Status != null && x.Status != "")
                    .Select(x => x.Status).Distinct().OrderBy(x => x).ToList(),

                Companies = context.WebOrders
                    .Where(x => x.CompanyID != null && x.CompanyID != "")
                    .Select(x => x.CompanyID).Distinct().OrderBy(x => x).ToList(),

                Treatments = context.WebOrders
                    .Where(x => x.TreatmentDescription != null && x.TreatmentDescription != "")
                    .Select(x => x.TreatmentDescription).Distinct().OrderBy(x => x).ToList()
            };

            return Ok(options);
        }

        [HttpGet("doctor-stats")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<DoctorStats> GetDoctorStats(
            [FromQuery] string doctor,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            if (string.IsNullOrWhiteSpace(doctor))
                return BadRequest("Doctor is required");

            var prevFromDate = fromDate.AddYears(-1);
            var prevToDate = toDate.AddYears(-1);

            var orders = context.WebOrders
                .Where(x => x.Doctor == doctor &&
                            x.Ordered >= fromDate &&
                            x.Ordered <= toDate &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")))
                .OrderByDescending(x => x.Ordered)
                .ToList();

            var prevOrders = context.WebOrders
                .Where(x => x.Doctor == doctor &&
                            x.Ordered >= prevFromDate &&
                            x.Ordered <= prevToDate &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")))
                .ToList();

            var prevQNT = prevOrders.Sum(x => x.QNT ?? 0);

            double qntTrend = prevQNT > 0
                ? Math.Round((double)(orders.Sum(x => x.QNT ?? 0) - prevQNT) / prevQNT * 100, 1)
                : 0;

            var totalAllOrders = context.WebOrders
                .Where(x => x.Ordered >= fromDate && x.Ordered <= toDate)
                .Count();

            var totalAllOrdersPrev = context.WebOrders
                .Where(x => x.Ordered >= prevFromDate && x.Ordered <= prevToDate)
                .Count();

            double currentPct = totalAllOrders > 0
                ? Math.Round((double)orders.Count / totalAllOrders * 100, 1) : 0;
            double prevPct = totalAllOrdersPrev > 0
                ? Math.Round((double)prevOrders.Count / totalAllOrdersPrev * 100, 1) : 0;
            double trendPct = Math.Round(currentPct - prevPct, 1);

            var currentPatients = orders
                .Where(x => x.Patient != null)
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var existingPatients = context.WebOrders
                .Where(x => x.Doctor == doctor &&
                            x.Ordered < fromDate &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")) &&
                            x.Patient != null)
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var newPatients = currentPatients.Except(existingPatients).Count();

            var stats = new DoctorStats
            {
                Doctor = doctor,
                NewPatients = newPatients,
                TotalOrders = orders.Count,
                TotalQNT = orders.Sum(x => x.QNT ?? 0),
                UniquePatients = orders.Where(x => x.Patient != null).Select(x => x.Patient).Distinct().Count(),
                TotalAllOrders = totalAllOrders,
                SharePercent = currentPct,
                TrendPercent = trendPct,
                PrevTotalQNT = prevQNT,
                QNTTrendPercent = qntTrend,
                Orders = orders,
                PerMonth = orders
                    .Where(x => x.Ordered.HasValue)
                    .GroupBy(x => new { x.Ordered!.Value.Year, x.Ordered.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new MonthlyCount
                    {
                        Month = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Count = g.Count()
                    }).ToList(),
                PerMonthPrev = prevOrders
                    .Where(x => x.Ordered.HasValue)
                    .GroupBy(x => new { x.Ordered!.Value.Year, x.Ordered.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new MonthlyCount
                    {
                        Month = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Count = g.Count()
                    }).ToList(),
                PerStatus = orders
                    .Where(x => x.Status != null)
                    .GroupBy(x => x.Status!)
                    .Select(g => new StatusCount
                    {
                        Status = g.Key,
                        StatusLabel = new WebOrder { Status = g.Key }.StatusLabel,
                        Color = StatusHexColor(g.Key),
                        Count = g.Count()
                    }).ToList(),
                PerProduct = orders
                    .Where(x => x.TreatmentDescription != null)
                    .GroupBy(x => x.TreatmentDescription!)
                    .Select(g => new ProductCount
                    {
                        Product = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList()
            };

            return Ok(stats);
        }

        // ---- Κατάταξη γιατρών βάσει εμβολίων (BELTA/STALORAL) στην περίοδο ----
        [HttpGet("doctor-summary")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<List<DoctorSummaryRow>> GetDoctorSummary(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            // CompanyID στα WebOrders: "1" = SM, "2" = BM (βλ. WebOrder.CompanyLabel)
            var baseRows = context.WebOrders
                .Where(x => x.Ordered >= fromDate && x.Ordered <= toDate &&
                            x.Doctor != null && x.Doctor != "" &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")))
                .GroupBy(x => x.Doctor)
                .Select(g => new DoctorSummaryRow
                {
                    Doctor = g.Key!,
                    TotalOrders = g.Count(),
                    QtySM = g.Where(x => x.CompanyID == "1").Sum(x => x.QNT ?? 0),
                    QtyBM = g.Where(x => x.CompanyID == "2").Sum(x => x.QNT ?? 0),
                    QtyTotal = g.Sum(x => x.QNT ?? 0)
                })
                .OrderByDescending(x => x.QtyTotal)
                .ToList();

            // Σύνολα ίδιας περιόδου προηγούμενου έτους, για ένδειξη τάσης
            // (ίδια σύμβαση με το doctor-stats)
            var prevFromDate = fromDate.AddYears(-1);
            var prevToDate = toDate.AddYears(-1);
            var prevTotals = context.WebOrders
                .Where(x => x.Ordered >= prevFromDate && x.Ordered <= prevToDate &&
                            x.Doctor != null && x.Doctor != "" &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")))
                .GroupBy(x => x.Doctor)
                .Select(g => new { Doctor = g.Key!, Total = g.Sum(x => x.QNT ?? 0) })
                .ToList()
                .ToDictionary(x => x.Doctor, x => x.Total);

            // Σύνολα πρικ ανά όνομα γιατρού από το άλλο σύστημα (DoctorOrders).
            // Best-effort ταύτιση με όνομα -- ΔΕΝ φιλτράρει τη λίστα, μόνο εμπλουτίζει.
            var prickPerName = context.DoctorOrders
                .Where(o => o.OrderDate >= fromDate && o.OrderDate <= toDate && o.DoctorID != null)
                .Join(context.Doctors, o => o.DoctorID, d => d.DoctorID,
                      (o, d) => new { o.OrderID, d.FullName })
                .Join(context.DoctorOrderLines.Where(l => l.LineStatus != "Cancelled"),
                      x => x.OrderID, l => l.OrderID,
                      (x, l) => new { x.FullName, Qty = l.QuantityRequested - l.QuantityCancelled })
                .GroupBy(x => x.FullName)
                .Select(g => new { FullName = g.Key, Total = g.Sum(x => x.Qty) })
                .AsEnumerable()
                .GroupBy(x => DoctorNameKey.Normalize(x.FullName))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

            foreach (var row in baseRows)
            {
                row.PrevQtyTotal = prevTotals.TryGetValue(row.Doctor, out var p) ? p : 0;
                row.PrickQtyTotal = prickPerName.TryGetValue(DoctorNameKey.Normalize(row.Doctor), out var q)
                    ? q : (int?)null;
            }

            return Ok(baseRows);
        }

        // ---- Μείγματα (Allergen) ανά είδος (TreatmentDescription), μόνο BELTA/STALORAL ----
        // Εξαιρούνται οι ακυρωμένες (Status "5") και οι παραγγελίες αποθήκης (Patient "A A").
        // Το Allergen είναι κείμενο της μορφής "P-093 5 mix grasses 50, \nP-012 Olive 50,".
        // Ταξινομούμε τα συστατικά ώστε το ίδιο μείγμα (ίδια αλλεργιογόνα + ίδια ποσοστά)
        // γραμμένο με άλλη σειρά να δίνει το ίδιο κείμενο.
        // Ο "ασθενής" των παραγγελιών αποθήκης. Στη βάση εμφανίζεται με διάφορες γραφές:
        // λατινικά ή ελληνικά Α, με ένα ή περισσότερα κενά, ή με κενό στο τέλος.
        // Γι' αυτό συγκρίνουμε ΧΩΡΙΣ κενά και δεχόμαστε και τους τέσσερις συνδυασμούς.
        private const string WarehouseAA = "AA";           // λατινικά
        private const string WarehouseGG = "ΑΑ";  // ελληνικά
        private const string WarehouseAG = "AΑ";
        private const string WarehouseGA = "ΑA";

        // Το ίδιο αλλεργιογόνο γράφεται άλλοτε "V-003" και άλλοτε "v-003". Το κλειδί
        // του στοκ πρέπει να ταιριάζει με τη γραμμή της παραγγελίας ανεξάρτητα από
        // κεφαλαία/πεζά -- αλλιώς το στοκ δεν κολλάει πουθενά και χάνεται.
        private sealed class MixKeyComparer : IEqualityComparer<(string Treatment, string Mix)>
        {
            public static readonly MixKeyComparer Instance = new();

            public bool Equals((string Treatment, string Mix) a, (string Treatment, string Mix) b) =>
                string.Equals(a.Treatment, b.Treatment, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(a.Mix, b.Mix, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((string Treatment, string Mix) key) =>
                HashCode.Combine(key.Treatment.ToUpperInvariant(), key.Mix.ToUpperInvariant());
        }

        private static string NormalizeMix(string? allergen)
        {
            if (string.IsNullOrWhiteSpace(allergen))
                return TreatmentMixRow.NoMixLabel;

            var parts = allergen
                .Split(',')
                .Select(p => System.Text.RegularExpressions.Regex.Replace(p, @"\s+", " ").Trim())
                // Οι κωδικοί γράφονται άλλοτε "V-003" και άλλοτε "v-003" -- πάντα κεφαλαία.
                .Select(p => System.Text.RegularExpressions.Regex.Replace(
                    p, @"\b[A-Za-z]-\d+\b", m => m.Value.ToUpperInvariant()))
                .Where(p => p.Length > 0)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return parts.Count == 0 ? TreatmentMixRow.NoMixLabel : string.Join(",\n", parts);
        }

        private List<TreatmentMixGroup> BuildTreatmentMixStats(DateTime fromDate, DateTime toDate, string? company,
            string? doctor = null)
        {
            // Το toDate έρχεται ως μεσάνυχτα -- μετράμε ολόκληρη την τελευταία μέρα
            var toExclusive = toDate.Date.AddDays(1);

            var query = context.WebOrders
                .Where(x => x.Ordered >= fromDate && x.Ordered < toExclusive &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")) &&
                            x.Status != "5" &&
                            // Εξαιρούνται οι παραγγελίες αποθήκης, σε όποια γραφή κι αν είναι.
                            (x.Patient == null ||
                             (x.Patient.Replace(" ", "") != WarehouseAA &&
                              x.Patient.Replace(" ", "") != WarehouseGG &&
                              x.Patient.Replace(" ", "") != WarehouseAG &&
                              x.Patient.Replace(" ", "") != WarehouseGA)));

            if (!string.IsNullOrWhiteSpace(company))
                query = query.Where(x => x.CompanyID == company);

            if (!string.IsNullOrWhiteSpace(doctor))
                query = query.Where(x => x.Doctor == doctor);

            var raw = query
                .Select(x => new { x.TreatmentDescription, x.Allergen, x.QNT })
                .ToList();

            // ---- Στοκ: οι παραγγελίες αποθήκης, χωρίς περιορισμό ημερομηνίας ----
            // Το στοκ είναι τωρινό, δεν αφορά την επιλεγμένη περίοδο. Το φίλτρο γιατρού
            // επίσης δεν ισχύει εδώ: το στοκ ανήκει στην εταιρεία, όχι σε γιατρό.
            // ΙΔΙΟ φίλτρο με τη σελίδα Stock: ασθενής αποθήκης + κατάσταση 2/3,
            // ΧΩΡΙΣ περιορισμό στο είδος. Ο περιορισμός BELTA/STALORAL έκρυβε το στοκ
            // όταν η περιγραφή του είδους γράφεται διαφορετικά στις γραμμές αποθήκης.
            var stockQuery = context.WebOrders
                .Where(x => x.TreatmentDescription != null &&
                            x.Patient != null &&
                            (x.Patient.Replace(" ", "") == WarehouseAA ||
                             x.Patient.Replace(" ", "") == WarehouseGG ||
                             x.Patient.Replace(" ", "") == WarehouseAG ||
                             x.Patient.Replace(" ", "") == WarehouseGA) &&
                            (x.Status == "2" || x.Status == "3"));

            if (!string.IsNullOrWhiteSpace(company))
                stockQuery = stockQuery.Where(x => x.CompanyID == company);

            var stockRaw = stockQuery
                .AsNoTracking()
                .Select(x => new { x.TreatmentDescription, x.Allergen, x.QNT, x.Status })
                .ToList();

            // Status 3 = Received (είναι εδώ), Status 2 = Manufacturing (αναμένεται)
            int Here(IEnumerable<dynamic> rows) => rows.Where(r => (string?)r.Status == "3").Sum(r => (int?)r.QNT ?? 0);
            int Expected(IEnumerable<dynamic> rows) => rows.Where(r => (string?)r.Status == "2").Sum(r => (int?)r.QNT ?? 0);

            // Ο comparer μπαίνει ΚΑΙ στο GroupBy: αλλιώς δύο γραφές του ίδιου μείγματος
            // θα έδιναν δύο ομάδες και το ToDictionary θα έσκαγε με διπλό κλειδί.
            var stockByMix = stockRaw
                .GroupBy(x => (Treatment: x.TreatmentDescription!.Trim(), Mix: NormalizeMix(x.Allergen)),
                         MixKeyComparer.Instance)
                .ToDictionary(g => g.Key, g => (Here: Here(g), Expected: Expected(g)), MixKeyComparer.Instance);

            var stockByTreatment = stockRaw
                .GroupBy(x => x.TreatmentDescription!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (Here: Here(g), Expected: Expected(g)), StringComparer.OrdinalIgnoreCase);

            var groups = raw
                .GroupBy(x => x.TreatmentDescription!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new TreatmentMixGroup
                {
                    Treatment = g.Key,
                    TotalQNT = g.Sum(x => x.QNT ?? 0),
                    StockHere = stockByTreatment.TryGetValue(g.Key, out var st) ? st.Here : 0,
                    StockExpected = stockByTreatment.TryGetValue(g.Key, out var st2) ? st2.Expected : 0,
                    Mixes = g
                        .Select(x => new { Label = NormalizeMix(x.Allergen), x.QNT })
                        .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                        .Select(mg => new TreatmentMixRow
                        {
                            Allergen = mg.First().Label,
                            QNT = mg.Sum(x => x.QNT ?? 0),
                            StockHere = stockByMix.TryGetValue((g.Key, mg.First().Label), out var sm) ? sm.Here : 0,
                            StockExpected = stockByMix.TryGetValue((g.Key, mg.First().Label), out var sm2) ? sm2.Expected : 0
                        })
                        .ToList()
                })
                .ToList();

            // Στοκ που δεν αντιστοιχεί σε καμία παραγγελία της περιόδου πρέπει να
            // φαίνεται κι αυτό -- αλλιώς είδος χωρίς πωλήσεις στο διάστημα (π.χ. STALORAL)
            // θα εξαφανιζόταν, παρότι υπάρχει στην αποθήκη.
            foreach (var entry in stockByMix)
            {
                var (treatment, mix) = entry.Key;

                var group = groups.FirstOrDefault(x =>
                    string.Equals(x.Treatment, treatment, StringComparison.OrdinalIgnoreCase));

                if (group == null)
                {
                    // Νέο είδος μόνο από στοκ: δεκτό μόνο αν είναι όντως εμβόλιο,
                    // ώστε να μην μπουν εδώ προϊόντα prick ή αναλώσιμα.
                    if (!treatment.StartsWith("BELTA", StringComparison.OrdinalIgnoreCase) &&
                        !treatment.StartsWith("STALORAL", StringComparison.OrdinalIgnoreCase))
                        continue;

                    group = new TreatmentMixGroup
                    {
                        Treatment = treatment,
                        TotalQNT = 0,
                        StockHere = stockByTreatment.TryGetValue(treatment, out var tot) ? tot.Here : entry.Value.Here,
                        StockExpected = stockByTreatment.TryGetValue(treatment, out var tot2) ? tot2.Expected : entry.Value.Expected
                    };
                    groups.Add(group);
                }

                if (!group.Mixes.Any(m => string.Equals(m.Allergen, mix, StringComparison.OrdinalIgnoreCase)))
                    group.Mixes.Add(new TreatmentMixRow
                    {
                        Allergen = mix,
                        QNT = 0,
                        StockHere = entry.Value.Here,
                        StockExpected = entry.Value.Expected
                    });
            }

            // Η επικεφαλίδα βγαίνει ΑΠΟ τις γραμμές, όχι χωριστά: ό,τι κι αν συμβεί
            // παραπάνω, το σύνολο που βλέπει ο χρήστης αθροίζει πάντα τις γραμμές του.
            foreach (var g in groups)
            {
                g.StockHere = g.Mixes.Sum(m => m.StockHere);
                g.StockExpected = g.Mixes.Sum(m => m.StockExpected);
            }

            foreach (var g in groups)
                g.Mixes = g.Mixes
                    .OrderByDescending(m => m.QNT)
                    .ThenByDescending(m => m.StockQNT)
                    .ThenBy(m => m.Allergen)
                    .ToList();

            return groups.OrderBy(t => t.Treatment).ToList();
        }

        [HttpGet("treatment-mix-stats")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<List<TreatmentMixGroup>> GetTreatmentMixStats(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? company,
            [FromQuery] string? doctor)
        {
            return Ok(BuildTreatmentMixStats(fromDate, toDate, company, doctor));
        }

        [HttpGet("treatment-mix-stats-excel")]
        [Authorize(Policy = "NotWarehouse")]
        public IActionResult ExportTreatmentMixStats(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string? company,
            [FromQuery] string? doctor,
            [FromQuery] int securityMonths = 3)
        {
            var groups = BuildTreatmentMixStats(fromDate, toDate, company, doctor);
            var companyLabel = company == "1" ? "SM" : company == "2" ? "BM" : "SM + BM";
            var doctorLabel = string.IsNullOrWhiteSpace(doctor) ? "" : $"  --  {doctor}";

            // Ίδιοι υπολογισμοί με την οθόνη: ο μήνας είναι 30,44 ημέρες και ο Μ.Ο.
            // στρογγυλοποιείται ΠΡΙΝ πολλαπλασιαστεί, ώστε τα νούμερα να συμφωνούν.
            var months = Math.Max(((toDate.Date - fromDate.Date).TotalDays + 1) / 30.44, 1d);
            var coverMonths = Math.Clamp(securityMonths, 1, 24);
            int Avg(int total) => (int)Math.Round(total / months, MidpointRounding.AwayFromZero);
            int Need(int total, int here, int expected) =>
                Math.Max(0, Avg(total) * coverMonths - (here + expected));

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Μείγματα ανά είδος");

            const int lastCol = 7;

            // ── ΤΙΤΛΟΣ ──
            ws.Cell(1, 1).Value = $"Μείγματα ανά είδος ({companyLabel})  {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}{doctorLabel}  --  απόθεμα ασφαλείας για {coverMonths} μήνες";
            ws.Range(1, 1, 1, lastCol).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 13;

            // ── HEADERS ──
            const int headerRow = 3;
            var headers = new[] { "ΕΙΔΟΣ", "ΜΕΙΓΜΑ", "ΠΟΣΟΤΗΤΑ", "Μ.Ο./ΜΗΝΑ", "ΕΔΩ", "ΑΝΑΜΕΝΟΝΤΑΙ", "ΑΣΦΑΛΕΙΑΣ" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = headerRow + 1;

            foreach (var g in groups)
            {
                int alt = 0;
                foreach (var m in g.Mixes)
                {
                    var need = Need(m.QNT, m.StockHere, m.StockExpected);
                    ws.Cell(row, 1).Value = g.Treatment;
                    ws.Cell(row, 2).Value = m.Allergen;
                    ws.Cell(row, 3).Value = m.QNT;
                    ws.Cell(row, 4).Value = Avg(m.QNT);
                    ws.Cell(row, 5).Value = m.StockHere;
                    ws.Cell(row, 6).Value = m.StockExpected;
                    ws.Cell(row, 7).Value = need;
                    if (need > 0)
                    {
                        ws.Cell(row, 7).Style.Font.Bold = true;
                        ws.Cell(row, 7).Style.Font.FontColor = XLColor.FromHtml("#C00000");
                    }
                    ws.Range(row, 1, row, lastCol).Style.Fill.BackgroundColor = alt++ % 2 == 0
                        ? XLColor.White
                        : XLColor.FromHtml("#EBF3FB");
                    row++;
                }

                // ── ΥΠΟΣΥΝΟΛΟ ΕΙΔΟΥΣ ──
                ws.Range(row, 1, row, 2).Merge();
                ws.Cell(row, 1).Value = $"Σύνολο {g.Treatment}";
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(row, 3).Value = g.TotalQNT;
                ws.Cell(row, 4).Value = Avg(g.TotalQNT);
                ws.Cell(row, 5).Value = g.StockHere;
                ws.Cell(row, 6).Value = g.StockExpected;
                ws.Cell(row, 7).Value = g.Mixes.Sum(m => Need(m.QNT, m.StockHere, m.StockExpected));
                var subRange = ws.Range(row, 1, row, lastCol);
                subRange.Style.Font.Bold = true;
                subRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#D6E4F0");
                row++;
            }

            // ── GRAND TOTAL ──
            ws.Range(row, 1, row, 2).Merge();
            ws.Cell(row, 1).Value = "ΓΕΝΙΚΟ ΣΥΝΟΛΟ";
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(row, 3).Value = groups.Sum(g => g.TotalQNT);
            ws.Cell(row, 4).Value = Avg(groups.Sum(g => g.TotalQNT));
            ws.Cell(row, 5).Value = groups.Sum(g => g.StockHere);
            ws.Cell(row, 6).Value = groups.Sum(g => g.StockExpected);
            ws.Cell(row, 7).Value = groups.Sum(g => g.Mixes.Sum(m => Need(m.QNT, m.StockHere, m.StockExpected)));
            var totalRange = ws.Range(row, 1, row, lastCol);
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
            totalRange.Style.Font.FontColor = XLColor.White;
            totalRange.Style.Font.Bold = true;

            // ── ΓΡΑΜΜΑΤΟΣΕΙΡΑ / ΣΤΟΙΧΙΣΗ / BORDERS ──
            ws.Range(1, 1, row, lastCol).Style.Font.FontName = "Arial";
            ws.Range(headerRow + 1, 3, row, lastCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(headerRow, 1, row, lastCol).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, row, lastCol).Style.Border.InsideBorder = XLBorderStyleValues.Hair;

            // ── COLUMN WIDTHS ──
            ws.Columns(1, lastCol).AdjustToContents(headerRow, row);
            ws.Column(1).Width = Math.Min(Math.Max(ws.Column(1).Width, 30), 60);
            ws.Column(2).Width = Math.Min(Math.Max(ws.Column(2).Width, 30), 80);
            ws.Column(2).Style.Alignment.WrapText = true;
            for (int c = 3; c <= lastCol; c++)
                ws.Column(c).Width = Math.Max(ws.Column(c).Width, 12);

            ws.SheetView.FreezeRows(headerRow);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Μείγματα_ανά_είδος_{fromDate:dd-MM-yyyy}_{toDate:dd-MM-yyyy}.xlsx");
        }

        // ---- Σύγκριση ετών: πάντα 12 μήνες, μία σειρά ανά χρονιά ----
        // Ανεξάρτητο από τα φίλτρα ημερομηνίας της αρχικής -- εδώ διαλέγεις χρονιές.
        [HttpGet("company-stats-yearly")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<YearlyMonthlyStats> GetYearlyMonthlyStats(
            [FromQuery] string? company,
            [FromQuery] string? serverFilter,
            [FromQuery] string? years)
        {
            var baseQuery = context.WebOrders
                .Where(x => x.Ordered != null &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")));

            if (!string.IsNullOrWhiteSpace(company))
                baseQuery = baseQuery.Where(x => x.CompanyID == company);

            if (!string.IsNullOrWhiteSpace(serverFilter))
            {
                var companyId = serverFilter == "SM" ? "1" : "2";
                baseQuery = baseQuery.Where(x => x.CompanyID == companyId);
            }

            // Η ομαδοποίηση γίνεται ΣΤΗ ΒΑΣΗ: επιστρέφονται λίγες δεκάδες γραμμές
            // (χρονιά x μήνας) αντί για όλες τις παραγγελίες όλων των ετών.
            var grouped = baseQuery
                .AsNoTracking()
                .GroupBy(x => new { x.Ordered!.Value.Year, x.Ordered.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToList();

            var availableYears = grouped.Select(g => g.Year).Distinct().OrderByDescending(y => y).ToList();

            // Προαιρετικό φίλτρο ετών. Χωρίς αυτό επιστρέφονται όλες οι χρονιές --
            // το ωφέλιμο φορτίο είναι μικρό και ο χρήστης αλλάζει επιλογή χωρίς νέα κλήση.
            var requested = (years ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(y => int.TryParse(y, out var v) ? v : 0)
                .Where(y => y > 0)
                .Distinct()
                .ToList();

            var wanted = requested.Count > 0 ? requested : availableYears;

            var series = wanted
                .OrderBy(y => y)
                .Select(year =>
                {
                    var counts = new int[12];
                    foreach (var g in grouped.Where(g => g.Year == year))
                        counts[g.Month - 1] = g.Count;

                    return new YearSeries
                    {
                        Year = year,
                        Counts = counts.ToList(),
                        Total = counts.Sum()
                    };
                })
                .ToList();

            return Ok(new YearlyMonthlyStats { AvailableYears = availableYears, Series = series });
        }

        [HttpGet("company-stats")]
        [Authorize(Policy = "NotWarehouse")]
        public ActionResult<CompanyStats> GetCompanyStats(
            [FromQuery] string? company,
            [FromQuery] string? serverFilter,
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate)
        {
            var prevFromDate = fromDate.AddYears(-1);
            var prevToDate = toDate.AddYears(-1);

            var baseQuery = context.WebOrders
                .Where(x => x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")));

            if (!string.IsNullOrWhiteSpace(company))
                baseQuery = baseQuery.Where(x => x.CompanyID == company);

            if (!string.IsNullOrWhiteSpace(serverFilter))
            {
                var companyId = serverFilter == "SM" ? "1" : "2";
                baseQuery = baseQuery.Where(x => x.CompanyID == companyId);
            }

            var orders = baseQuery
                .Where(x => x.Ordered >= fromDate && x.Ordered <= toDate)
                .OrderByDescending(x => x.Ordered)
                .AsNoTracking()
                .Select(x => new StatRow
                {
                    Patient = x.Patient,
                    CompanyID = x.CompanyID,
                    TreatmentDescription = x.TreatmentDescription,
                    QNT = x.QNT,
                    Ordered = x.Ordered
                })
                .ToList();

            var prevOrders = baseQuery
                .Where(x => x.Ordered >= prevFromDate && x.Ordered <= prevToDate)
                .AsNoTracking()
                .Select(x => new StatRow
                {
                    Patient = x.Patient,
                    CompanyID = x.CompanyID,
                    TreatmentDescription = x.TreatmentDescription,
                    QNT = x.QNT,
                    Ordered = x.Ordered
                })
                .ToList();

            var prevQNT = prevOrders.Sum(x => x.QNT ?? 0);

            double qntTrend = prevQNT > 0
                ? Math.Round((double)(orders.Sum(x => x.QNT ?? 0) - prevQNT) / prevQNT * 100, 1)
                : 0;

            var totalAllOrders = context.WebOrders
                .Where(x => x.Ordered >= fromDate && x.Ordered <= toDate)
                .Count();

            var totalAllOrdersPrev = context.WebOrders
                .Where(x => x.Ordered >= prevFromDate && x.Ordered <= prevToDate)
                .Count();

            double currentPct = totalAllOrders > 0
                ? Math.Round((double)orders.Count / totalAllOrders * 100, 1) : 0;
            double prevPct = totalAllOrdersPrev > 0
                ? Math.Round((double)prevOrders.Count / totalAllOrdersPrev * 100, 1) : 0;
            double trendPct = Math.Round(currentPct - prevPct, 1);

            var currentPatients = orders
                .Where(x => x.Patient != null)
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var existingPatientsQuery = context.WebOrders
                .Where(x => x.Ordered < fromDate &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")) &&
                            x.Patient != null);

            if (!string.IsNullOrWhiteSpace(company))
                existingPatientsQuery = existingPatientsQuery.Where(x => x.CompanyID == company);

            if (!string.IsNullOrWhiteSpace(serverFilter))
            {
                var companyId = serverFilter == "SM" ? "1" : "2";
                existingPatientsQuery = existingPatientsQuery.Where(x => x.CompanyID == companyId);
            }

            var existingPatients = existingPatientsQuery
                .AsNoTracking()
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var newPatients = currentPatients.Except(existingPatients).Count();

            // Ασθενείς προηγούμενης ισόχρονης περιόδου + τάση (αντίστοιχο του Προηγ. ΤΕΜ/Τάση)
            var prevPatients = prevOrders
                .Where(x => x.Patient != null)
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();
            var prevUniquePatients = prevPatients.Count;
            double patientsTrend = prevUniquePatients > 0
                ? Math.Round((double)(currentPatients.Count - prevUniquePatients) / prevUniquePatients * 100, 1)
                : 0;

            // Νέοι ασθενείς της προηγούμενης περιόδου: όσοι δεν είχαν εμφανιστεί πριν το prevFromDate
            var existingBeforePrevQuery = context.WebOrders
                .Where(x => x.Ordered < prevFromDate &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")) &&
                            x.Patient != null);
            if (!string.IsNullOrWhiteSpace(company))
                existingBeforePrevQuery = existingBeforePrevQuery.Where(x => x.CompanyID == company);
            if (!string.IsNullOrWhiteSpace(serverFilter))
            {
                var companyId = serverFilter == "SM" ? "1" : "2";
                existingBeforePrevQuery = existingBeforePrevQuery.Where(x => x.CompanyID == companyId);
            }
            var existingBeforePrev = existingBeforePrevQuery.AsNoTracking().Select(x => x.Patient!).Distinct().ToList();
            var prevNewPatients = prevPatients.Except(existingBeforePrev).Count();

            // Ανάλυση παραγγελιών ανά εταιρεία + τάση πλήθους παραγγελιών
            var currentOrdersSM = orders.Count(x => x.CompanyID == "1");
            var currentOrdersBM = orders.Count(x => x.CompanyID == "2");
            var prevOrdersSM = prevOrders.Count(x => x.CompanyID == "1");
            var prevOrdersBM = prevOrders.Count(x => x.CompanyID == "2");
            double ordersTrend = prevOrders.Count > 0
                ? Math.Round((double)(orders.Count - prevOrders.Count) / prevOrders.Count * 100, 1)
                : 0;

            // ── Νέοι ασθενείς με POLYMERISED θεραπεία ──
            var polymerizedOrders = orders
                .Where(x => x.Patient != null &&
                            x.TreatmentDescription != null &&
                            x.TreatmentDescription.Contains("POLYMERIS"))
                .ToList();

            var polymerizedPatients = polymerizedOrders
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var newPolymerizedPatients = polymerizedPatients
                .Except(existingPatients)
                .ToList();

            var newPolymerizedQNT = polymerizedOrders
                .Where(x => newPolymerizedPatients.Contains(x.Patient!))
                .Sum(x => x.QNT ?? 0);

            // ── Breakdown ανά POLYMERISED θεραπεία (τεμάχια νέων ασθενών) ──
            var polymerizedProducts = polymerizedOrders
                .Where(x => newPolymerizedPatients.Contains(x.Patient!))
                .GroupBy(x => x.TreatmentDescription!)
                .Select(g => new ProductCount
                {
                    Product = g.Key,
                    Count = g.Sum(x => x.QNT ?? 0)
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            var stats = new CompanyStats
            {
                Company = company ?? "ALL",
                TotalOrders = orders.Count,
                TotalQNT = orders.Sum(x => x.QNT ?? 0),
                UniquePatients = orders.Where(x => x.Patient != null).Select(x => x.Patient).Distinct().Count(),
                NewPatients = newPatients,
                TotalAllOrders = totalAllOrders,
                SharePercent = currentPct,
                TrendPercent = trendPct,
                PrevTotalQNT = prevQNT,
                QNTTrendPercent = qntTrend,
                PrevUniquePatients = prevUniquePatients,
                PatientsTrendPercent = patientsTrend,
                CurrentOrdersSM = currentOrdersSM,
                CurrentOrdersBM = currentOrdersBM,
                PrevOrdersSM = prevOrdersSM,
                PrevOrdersBM = prevOrdersBM,
                PrevTotalOrders = prevOrders.Count,
                OrdersTrendPercent = ordersTrend,
                PrevNewPatients = prevNewPatients,
                NewPolymerizedPatients = newPolymerizedPatients.Count,
                NewPolymerizedQNT = newPolymerizedQNT,
                PolymerizedProducts = polymerizedProducts,
                PerMonth = orders
                    .Where(x => x.Ordered.HasValue)
                    .GroupBy(x => new { x.Ordered!.Value.Year, x.Ordered.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new MonthlyCount
                    {
                        Month = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Count = g.Count()
                    }).ToList(),
                PerMonthPrev = prevOrders
                    .Where(x => x.Ordered.HasValue)
                    .GroupBy(x => new { x.Ordered!.Value.Year, x.Ordered.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new MonthlyCount
                    {
                        Month = $"{g.Key.Month:D2}/{g.Key.Year}",
                        Count = g.Count()
                    }).ToList(),
                PerProduct = orders
                    .Where(x => x.TreatmentDescription != null)
                    .GroupBy(x => x.TreatmentDescription!)
                    .Select(g => new ProductCount
                    {
                        Product = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList()
            };

            return Ok(stats);
        }

        // Μόνο τα πεδία που χρησιμοποιούν τα στατιστικά της αρχικής.
        private class StatRow
        {
            public string? Patient { get; set; }
            public string? CompanyID { get; set; }
            public string? TreatmentDescription { get; set; }
            public int? QNT { get; set; }
            public DateTime? Ordered { get; set; }
        }

        private static string StatusHexColor(string status) => status switch
        {
            "1" => "#6c757d",
            "2" => "#ffc107",
            "3" => "#0dcaf0",
            "4" => "#0d6efd",
            "5" => "#dc3545",
            "11" => "#198754",
            _ => "#adb5bd"
        };
    }
}
