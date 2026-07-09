using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PrickDashboardController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PrickDashboardController(StallmedContext context)
        {
            _context = context;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<PrickDashboardSummaryDto>> GetSummary([FromQuery] string? company)
        {
            var dto = new PrickDashboardSummaryDto();

            // ---- KPI: εκκρεμείς γραμμές παραγγελιών γιατρών ----
            var doctorLinesQuery = _context.DoctorOrderLines
                .Include(l => l.Order)
                .Where(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");
            if (!string.IsNullOrEmpty(company))
                doctorLinesQuery = doctorLinesQuery.Where(l => l.Order.Company == company);

            dto.PendingDoctorOrderLinesCount = await doctorLinesQuery.CountAsync();

            // ---- KPI: ανοιχτές παραγγελίες παραγωγής ----
            var prodQuery = _context.ProductionOrders
                .Where(p => p.Status == "Open" || p.Status == "PartiallyReceived");
            if (!string.IsNullOrEmpty(company))
                prodQuery = prodQuery.Where(p => p.Company == company);
            dto.OpenProductionOrdersCount = await prodQuery.CountAsync();

            // ---- Πίνακας τρέχοντος stock (ανά κωδικό+τύπο) ----
            var stockRaw = await _context.StockReceipts
                .Where(r => !r.IsDepleted)
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            foreach (var s in stockRaw)
            {
                allergenLookup.TryGetValue(s.CodePrick, out var allergen);
                productLookup.TryGetValue(s.ProductTypeCode, out var product);

                var stockCompany = allergen?.Company ?? product?.Company;
                if (!string.IsNullOrEmpty(company) && stockCompany != null && stockCompany != company)
                    continue;

                dto.StockLevels.Add(new PrickStockLevelDto
                {
                    CodePrick = s.CodePrick,
                    AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = s.ProductTypeCode,
                    ProductDescription = product?.Description,
                    Company = stockCompany,
                    TotalRemaining = s.Total
                });
            }
            dto.StockLevels = dto.StockLevels.OrderBy(x => x.CodePrick).ToList();

            // ---- Aging: 30 παλαιότερες εκκρεμείς γραμμές ----
            var agingQuery = _context.DoctorOrderLines
                .Include(l => l.Order).ThenInclude(o => o.Doctor)
                .Where(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");
            if (!string.IsNullOrEmpty(company))
                agingQuery = agingQuery.Where(l => l.Order.Company == company);

            var agingRaw = await agingQuery
                .OrderBy(l => l.Order.OrderDate)
                .Take(30)
                .ToListAsync();

            dto.AgingLines = agingRaw.Select(l => new PrickAgingLineDto
            {
                OrderCode = l.Order.OrderCode,
                DoctorName = l.Order.Doctor != null ? l.Order.Doctor.FullName : l.Order.DoctorName,
                CodePrick = l.CodePrick,
                ProductTypeCode = l.ProductTypeCode,
                QuantityRequested = l.QuantityRequested,
                QuantityAllocated = l.QuantityAllocated,
                QuantityCancelled = l.QuantityCancelled,
                OrderDate = l.Order.OrderDate,
                DaysPending = (int)(DateTime.Today - l.Order.OrderDate).TotalDays
            }).ToList();

            return Ok(dto);
        }

        // ---- Πρόταση Παραγγελίας Στοκ ----
        // Security stock = μέσος εβδομαδιαίος όγκος παραγγελιών (τελευταίοι 12 εβδομάδες) × 2
        // Πρόταση = max(0, PendingDemand + SecurityStock - CurrentStock)
        [HttpGet("stock-order-proposal")]
        public async Task<ActionResult<List<StockOrderProposalItemDto>>> GetStockOrderProposal(
            [FromQuery] string? company)
        {
            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            // Τρέχον stock ανά κωδικό+τύπος
            // (το stock δεν έχει άμεσο company πεδίο - εμφανίζουμε συνολικά)
            var stockFiltered = await _context.StockReceipts
                .Where(r => !r.IsDepleted && r.QuantityRemaining > 0)
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Company = "", Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();

            // Εκκρεμής ζήτηση (Open doctor order lines)
            var pendingQuery = _context.DoctorOrderLines
                .Include(l => l.Order)
                .Where(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");
            if (!string.IsNullOrEmpty(company))
                pendingQuery = pendingQuery.Where(l => l.Order.Company == company);

            var pending = await pendingQuery
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.Order.Company })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, g.Key.Company,
                    Total = g.Sum(l => l.QuantityRequested - l.QuantityAllocated - l.QuantityCancelled) })
                .ToListAsync();

            // Ιστορικός μέσος όρος παραγγελιών (τελευταίες 12 εβδομάδες)
            var since = DateTime.Today.AddDays(-84);
            var histQuery = _context.DoctorOrderLines
                .Include(l => l.Order)
                .Where(l => l.Order.OrderDate >= since);
            if (!string.IsNullOrEmpty(company))
                histQuery = histQuery.Where(l => l.Order.Company == company);

            var history = await histQuery
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.Order.Company })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, g.Key.Company,
                    Total = g.Sum(l => l.QuantityRequested) })
                .ToListAsync();

            // Συνδυασμός — βάση είναι η ζήτηση (pending + history) ανά εταιρεία
            var keys = pending.Select(x => (x.CodePrick, x.ProductTypeCode, x.Company))
                .Union(history.Select(x => (x.CodePrick, x.ProductTypeCode, x.Company)))
                .Distinct().ToList();

            var result = new List<StockOrderProposalItemDto>();
            foreach (var key in keys)
            {
                // Stock είναι κοινό (δεν ξεχωρίζει εταιρεία)
                var currentStock = stockFiltered.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode)?.Total ?? 0;
                var pendingDemand = pending.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode && x.Company == key.Company)?.Total ?? 0;
                var histTotal = history.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode && x.Company == key.Company)?.Total ?? 0;
                var weeklyAvg = histTotal / 12.0;
                var securityStock = (int)Math.Ceiling(weeklyAvg * 2);
                var proposed = Math.Max(0, pendingDemand + securityStock - currentStock);

                if (proposed == 0 && pendingDemand == 0) continue; // παράλειψη αν δεν χρειάζεται τίποτα

                allergenLookup.TryGetValue(key.CodePrick, out var allergen);
                productLookup.TryGetValue(key.ProductTypeCode, out var product);

                result.Add(new StockOrderProposalItemDto
                {
                    CodePrick = key.CodePrick,
                    AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = key.ProductTypeCode,
                    ProductDescription = product?.Description,
                    Company = key.Company,
                    CurrentStock = currentStock,
                    PendingDemand = pendingDemand,
                    SecurityStock = securityStock,
                    Proposed = proposed,
                    OrderQuantity = proposed
                });
            }

            return Ok(result.OrderBy(x => x.Company).ThenBy(x => x.CodePrick).ToList());
        }
        // ---- Export πρότασης παραγγελίας σε Excel ----
        [HttpPost("stock-order-export")]
        public ActionResult ExportStockOrderExcel([FromQuery] string company, [FromBody] List<StockOrderProposalItemDto> items)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add($"StockOrder_{company}");
            ws.Cell(1, 1).Value = "Κωδικός";
            ws.Cell(1, 2).Value = "Περιγραφή";
            ws.Cell(1, 3).Value = "Τύπος";
            ws.Cell(1, 4).Value = "Ποσότητα";
            ws.Range(1, 1, 1, 4).Style.Font.SetBold();

            int row = 2;
            foreach (var item in items.Where(x => x.OrderQuantity > 0))
            {
                ws.Cell(row, 1).Value = item.CodePrick;
                ws.Cell(row, 2).Value = item.AllergenDescription;
                ws.Cell(row, 3).Value = item.ProductTypeCode;
                ws.Cell(row, 4).Value = item.OrderQuantity;
                row++;
            }
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"StockOrder_{company}_{DateTime.Today:yyyyMMdd}.xlsx");
        }
    }
}
