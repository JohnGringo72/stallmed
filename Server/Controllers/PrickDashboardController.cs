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
    }
}
