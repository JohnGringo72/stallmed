using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                .ToList();

            var prevOrders = baseQuery
                .Where(x => x.Ordered >= prevFromDate && x.Ordered <= prevToDate)
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
                .Select(x => x.Patient!)
                .Distinct()
                .ToList();

            var newPatients = currentPatients.Except(existingPatients).Count();

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
