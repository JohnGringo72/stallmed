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

        // ---- Live Stock: τρέχον stock ανά κωδικό+τύπο+εταιρεία ----
        [HttpGet("live-stock")]
        public async Task<ActionResult<List<LiveStockItemDto>>> GetLiveStock(
            [FromQuery] string? company,
            [FromQuery] string? productTypeCode,
            [FromQuery] string? search)
        {
            var receiptsRaw = await (
                from r in _context.StockReceipts
                join pol in _context.ProductionOrderLines on r.ProductionOrderLineID equals pol.ProductionOrderLineID into polJoin
                from pol in polJoin.DefaultIfEmpty()
                join po in _context.ProductionOrders on pol.ProductionOrderID equals po.ProductionOrderID into poJoin
                from po in poJoin.DefaultIfEmpty()
                select new
                {
                    r.CodePrick,
                    r.ProductTypeCode,
                    r.QuantityReceived,
                    r.QuantityRemaining,
                    Company = po != null ? po.Company : null
                }
            ).ToListAsync();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            var items = receiptsRaw
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode, r.Company })
                .Select(g =>
                {
                    allergenLookup.TryGetValue(g.Key.CodePrick, out var allergen);
                    productLookup.TryGetValue(g.Key.ProductTypeCode, out var product);
                    return new LiveStockItemDto
                    {
                        CodePrick = g.Key.CodePrick,
                        AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                        ProductTypeCode = g.Key.ProductTypeCode,
                        ProductDescription = product?.Description,
                        Company = g.Key.Company,
                        TotalReceived = g.Sum(x => x.QuantityReceived),
                        TotalAllocated = g.Sum(x => x.QuantityReceived - x.QuantityRemaining),
                        FreeStock = g.Sum(x => x.QuantityRemaining)
                    };
                })
                .AsEnumerable();

            if (!string.IsNullOrEmpty(company))
                items = items.Where(x => x.Company == company);

            if (!string.IsNullOrEmpty(productTypeCode))
                items = items.Where(x => x.ProductTypeCode == productTypeCode);

            if (!string.IsNullOrEmpty(search))
                items = items.Where(x =>
                    (x.CodePrick?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.AllergenDescription?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));

            return Ok(items.OrderBy(x => x.CodePrick).ToList());
        }

        // ---- Λίστα ενεργών τύπων προϊόντος (για το φίλτρο) ----
        [HttpGet("product-types")]
        public async Task<ActionResult<List<ProductType>>> GetProductTypes()
        {
            var types = await _context.ProductTypes
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductTypeCode)
                .ToListAsync();
            return Ok(types);
        }

        // ---- Έξυπνη Πρόταση Παραγγελίας Στοκ ----
        // WeeklyAvg   = SUM(QuantityRequested από DoctorOrderLines τελευταίους 90 ημέρες) / 13 εβδομάδες
        // SecurityStock = WeeklyAvg × 12%
        // AlreadyOrdered = SUM(QuantityOrdered από ProductionOrderLines Open/PartiallyReceived) -- προσοχή:
        //   αθροίζει την ΠΛΗΡΗ παραγγελθείσα ποσότητα των γραμμών, όχι το εκκρεμές προς παραλαβή
        //   (QuantityOrdered - QuantityReceived), όπως ζητήθηκε ρητά.
        // PendingDemand = SUM(QuantityRequested - QuantityAllocated - QuantityCancelled) από Open DoctorOrderLines
        // CurrentStock  = SUM(QuantityRemaining από StockReceipts IsDepleted=0)
        // Proposed = MAX(0, PendingDemand + SecurityStock - CurrentStock - AlreadyOrdered)
        [HttpGet("smart-stock-proposal")]
        public async Task<ActionResult<List<SmartStockProposalDto>>> GetSmartStockProposal(
            [FromQuery] string? company, [FromQuery] string? productTypeCode)
        {
            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            // ΤρέχονStock: κοινό ανά κωδικό+τύπο (η φυσική αποθήκη δεν διαχωρίζεται ανά εταιρεία)
            var currentStockRaw = await _context.StockReceipts
                .Where(r => !r.IsDepleted)
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();

            // ΕκκρεμέςΑπόΓιατρούς: Open (Pending/PartiallyAllocated) DoctorOrderLines, ανά εταιρεία
            var pendingQuery = _context.DoctorOrderLines
                .Include(l => l.Order)
                .Where(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");
            if (!string.IsNullOrEmpty(company))
                pendingQuery = pendingQuery.Where(l => l.Order.Company == company);
            if (!string.IsNullOrEmpty(productTypeCode))
                pendingQuery = pendingQuery.Where(l => l.ProductTypeCode == productTypeCode);

            var pending = await pendingQuery
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.Order.Company })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, g.Key.Company,
                    Total = g.Sum(l => l.QuantityRequested - l.QuantityAllocated - l.QuantityCancelled) })
                .ToListAsync();

            // ΉδηΠαραγγελμένο: ProductionOrderLines σε Open/PartiallyReceived, ανά εταιρεία (μέσω ProductionOrder.Company)
            var alreadyOrderedQuery = _context.ProductionOrderLines
                .Include(l => l.ProductionOrder)
                .Where(l => l.LineStatus == "Open" || l.LineStatus == "PartiallyReceived");
            if (!string.IsNullOrEmpty(company))
                alreadyOrderedQuery = alreadyOrderedQuery.Where(l => l.ProductionOrder.Company == company);
            if (!string.IsNullOrEmpty(productTypeCode))
                alreadyOrderedQuery = alreadyOrderedQuery.Where(l => l.ProductTypeCode == productTypeCode);

            var alreadyOrdered = await alreadyOrderedQuery
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.ProductionOrder.Company })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, g.Key.Company,
                    Total = g.Sum(l => l.QuantityOrdered) })
                .ToListAsync();

            // ΜέσοςΌρος: SUM(QuantityRequested) τελευταίους 90 ημέρες (όλα τα statuses), ανά εταιρεία
            var since = DateTime.Today.AddDays(-90);
            var histQuery = _context.DoctorOrderLines
                .Include(l => l.Order)
                .Where(l => l.Order.OrderDate >= since);
            if (!string.IsNullOrEmpty(company))
                histQuery = histQuery.Where(l => l.Order.Company == company);
            if (!string.IsNullOrEmpty(productTypeCode))
                histQuery = histQuery.Where(l => l.ProductTypeCode == productTypeCode);

            var history = await histQuery
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.Order.Company })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, g.Key.Company,
                    Total = g.Sum(l => l.QuantityRequested) })
                .ToListAsync();

            // Ένωση κλειδιών (κωδικός+τύπος+εταιρεία) από όλες τις πηγές ζήτησης/παραγγελιοδοσίας
            var keys = pending.Select(x => (x.CodePrick, x.ProductTypeCode, x.Company))
                .Union(alreadyOrdered.Select(x => (x.CodePrick, x.ProductTypeCode, x.Company)))
                .Union(history.Select(x => (x.CodePrick, x.ProductTypeCode, x.Company)))
                .Distinct().ToList();

            var result = new List<SmartStockProposalDto>();
            foreach (var key in keys)
            {
                var currentStock = currentStockRaw.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode)?.Total ?? 0;
                var pendingDemand = pending.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode && x.Company == key.Company)?.Total ?? 0;
                var alreadyOrderedQty = alreadyOrdered.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode && x.Company == key.Company)?.Total ?? 0;
                var histTotal = history.FirstOrDefault(x => x.CodePrick == key.CodePrick && x.ProductTypeCode == key.ProductTypeCode && x.Company == key.Company)?.Total ?? 0;
                var weeklyAvg = histTotal / 13.0;
                var securityStock = (int)Math.Ceiling(weeklyAvg * 0.12);
                var proposed = Math.Max(0, pendingDemand + securityStock - currentStock - alreadyOrderedQty);

                if (proposed == 0 && pendingDemand == 0) continue; // παράλειψη αν δεν χρειάζεται τίποτα

                allergenLookup.TryGetValue(key.CodePrick, out var allergen);
                productLookup.TryGetValue(key.ProductTypeCode, out var product);

                result.Add(new SmartStockProposalDto
                {
                    CodePrick = key.CodePrick,
                    AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = key.ProductTypeCode,
                    ProductDescription = product?.Description,
                    Company = key.Company,
                    WeeklyAvg = (int)Math.Round(weeklyAvg),
                    SecurityStock = securityStock,
                    CurrentStock = currentStock,
                    AlreadyOrdered = alreadyOrderedQty,
                    PendingDemand = pendingDemand,
                    Proposed = proposed,
                    OrderQuantity = proposed
                });
            }

            return Ok(result.OrderBy(x => x.ProductTypeCode).ThenBy(x => x.CodePrick).ToList());
        }

        // ---- Export πρότασης παραγγελίας σε Excel (Κωδικός/Ποσότητα) ----
        [HttpPost("smart-stock-proposal-export")]
        public ActionResult ExportSmartStockProposalExcel(
            [FromQuery] string company, [FromQuery] string productTypeCode, [FromBody] List<SmartStockProposalDto> items)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add($"StockOrder_{company}_{productTypeCode}");
            ws.Cell(1, 1).Value = "Κωδικός";
            ws.Cell(1, 2).Value = "Ποσότητα";
            ws.Range(1, 1, 1, 2).Style.Font.SetBold();

            int row = 2;
            foreach (var item in items.Where(x => x.OrderQuantity > 0))
            {
                ws.Cell(row, 1).Value = item.CodePrick;
                ws.Cell(row, 2).Value = item.OrderQuantity;
                row++;
            }
            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"StockOrder_{company}_{productTypeCode}_{DateTime.Today:yyyyMMdd}.xlsx");
        }
    }
}
