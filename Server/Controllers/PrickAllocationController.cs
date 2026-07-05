using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using StallmedManager.Server.Models;
using StallmedManager.Shared.Models;
using System.Data;

namespace StallmedManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PrickAllocationController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PrickAllocationController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Λίστα εκκρεμών γραμμών παραγγελιών γιατρών + διαθέσιμο stock ----
        [HttpGet("pending")]
        public async Task<ActionResult<List<PendingOrderLineDto>>> GetPending([FromQuery] string? company, [FromQuery] int? doctorId)
        {
            var query = _context.DoctorOrderLines
                .Include(l => l.Order).ThenInclude(o => o.Doctor)
                .Where(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");

            if (!string.IsNullOrEmpty(company))
                query = query.Where(l => l.Order.Company == company);
            if (doctorId.HasValue)
                query = query.Where(l => l.Order.DoctorID == doctorId.Value);

            var lines = await query.OrderBy(l => l.Order.OrderDate).ToListAsync();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            // Διαθέσιμο stock ανά κωδικό+τύπο (μόνο για τους συνδυασμούς που χρειαζόμαστε)
            var neededPairs = lines.Select(l => (l.CodePrick, l.ProductTypeCode)).Distinct().ToList();
            var stockAgg = await _context.StockReceipts
                .Where(r => !r.IsDepleted)
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();
            var stockDict = stockAgg.ToDictionary(x => (x.CodePrick, x.ProductTypeCode), x => x.Total);

            var result = lines.Select(l =>
            {
                allergenLookup.TryGetValue(l.CodePrick, out var allergen);
                productLookup.TryGetValue(l.ProductTypeCode, out var product);
                stockDict.TryGetValue((l.CodePrick, l.ProductTypeCode), out var available);

                return new PendingOrderLineDto
                {
                    OrderLineID = l.OrderLineID,
                    OrderCode = l.Order.OrderCode,
                    DoctorName = l.Order.Doctor != null ? l.Order.Doctor.FullName : l.Order.DoctorName,
                    Company = l.Order.Company,
                    CodePrick = l.CodePrick,
                    AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = l.ProductTypeCode,
                    ProductDescription = product?.Description,
                    QuantityRequested = l.QuantityRequested,
                    QuantityAllocated = l.QuantityAllocated,
                    QuantityCancelled = l.QuantityCancelled,
                    AvailableStock = available,
                    OrderDate = l.Order.OrderDate
                };
            }).ToList();

            return Ok(result);
        }

        // ---- Dropdown: λίστα γιατρών ανά εταιρεία ----
        [HttpGet("doctors")]
        public async Task<ActionResult<List<DoctorOptionDto>>> GetDoctors([FromQuery] string? company)
        {
            var query = _context.DoctorOrders.AsQueryable();
            if (!string.IsNullOrEmpty(company))
                query = query.Where(o => o.Company == company);

            var doctorIds = await query
                .Where(o => o.DoctorID != null)
                .Select(o => o.DoctorID!.Value)
                .Distinct()
                .ToListAsync();

            var doctors = await _context.Doctors
                .Where(d => doctorIds.Contains(d.DoctorID))
                .OrderBy(d => d.FullName)
                .Select(d => new DoctorOptionDto { DoctorID = d.DoctorID, FullName = d.FullName })
                .ToListAsync();

            return Ok(doctors);
        }

        // ---- Ενεργές δεσμεύσεις (για reversal) ----
        [HttpGet("active")]
        public async Task<ActionResult<List<ActiveAllocationDto>>> GetActive([FromQuery] string? company, [FromQuery] long? orderLineId)
        {
            var allocs = await _context.OrderAllocations
                .Where(a => a.AllocationStatus == "Active")
                .ToListAsync();

            if (orderLineId.HasValue)
                allocs = allocs.Where(a => a.OrderLineID == orderLineId.Value).ToList();

            var lineIds = allocs.Select(a => a.OrderLineID).Distinct().ToList();
            var receiptIds = allocs.Select(a => a.ReceiptID).Distinct().ToList();

            var orderLines = await _context.DoctorOrderLines
                .Include(l => l.Order).ThenInclude(o => o.Doctor)
                .Where(l => lineIds.Contains(l.OrderLineID))
                .ToListAsync();
            var receipts = await _context.StockReceipts
                .Where(r => receiptIds.Contains(r.ReceiptID))
                .ToListAsync();
            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);

            if (!string.IsNullOrEmpty(company))
            {
                var validLineIds = orderLines.Where(l => l.Order.Company == company).Select(l => l.OrderLineID).ToHashSet();
                allocs = allocs.Where(a => validLineIds.Contains(a.OrderLineID)).ToList();
            }

            var result = allocs.Select(a =>
            {
                var line = orderLines.FirstOrDefault(l => l.OrderLineID == a.OrderLineID);
                var receipt = receipts.FirstOrDefault(r => r.ReceiptID == a.ReceiptID);
                allergenLookup.TryGetValue(line?.CodePrick ?? "", out var allergen);

                return new ActiveAllocationDto
                {
                    AllocationID = a.AllocationID,
                    OrderCode = line?.Order?.OrderCode,
                    DoctorName = line?.Order?.Doctor != null ? line.Order.Doctor.FullName : line?.Order?.DoctorName,
                    CodePrick = line?.CodePrick,
                    AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = line?.ProductTypeCode,
                    QuantityAllocated = a.QuantityAllocated,
                    AllocationDate = a.AllocationDate,
                    ReceiptDate = receipt?.ReceivedDate ?? default
                };
            })
            .OrderByDescending(x => x.AllocationDate)
            .Take(100)
            .ToList();

            return Ok(result);
        }

        // ---- Allocate (καλεί sp_AllocateStock) ----
        [HttpPost("allocate")]
        public async Task<ActionResult<AllocateResult>> Allocate([FromBody] AllocateRequest req)
        {
            var connection = (MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "sp_AllocateStock";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new MySqlParameter("p_OrderLineID", MySqlDbType.Int64) { Value = req.OrderLineID });
            cmd.Parameters.Add(new MySqlParameter("p_QuantityToAllocate", MySqlDbType.Int32) { Value = req.Quantity });
            cmd.Parameters.Add(new MySqlParameter("p_UserID", MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });

            var pAllocated = new MySqlParameter("p_QuantityActuallyAllocated", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(pAllocated);

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok(new AllocateResult { QuantityActuallyAllocated = Convert.ToInt32(pAllocated.Value) });
        }

        // ---- Reverse Allocation (καλεί sp_ReverseAllocation) ----
        [HttpPost("reverse")]
        public async Task<ActionResult> Reverse([FromBody] ReverseAllocationRequest req)
        {
            var connection = (MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "sp_ReverseAllocation";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new MySqlParameter("p_AllocationID", MySqlDbType.Int64) { Value = req.AllocationID });
            cmd.Parameters.Add(new MySqlParameter("p_UserID", MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
            cmd.Parameters.Add(new MySqlParameter("p_Reason", MySqlDbType.VarChar) { Value = (object?)req.Reason ?? DBNull.Value });

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (MySqlException ex)
            {
                return BadRequest(ex.Message);
            }

            return Ok();
        }
    }
}
