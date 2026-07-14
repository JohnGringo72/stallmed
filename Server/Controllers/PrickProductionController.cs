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
    public class PrickProductionController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PrickProductionController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Quick Stock Check: αναζήτηση διαθέσιμου stock ανά κωδικό/περιγραφή ----
        [HttpGet("stockcheck")]
        public async Task<ActionResult<List<StockCheckDto>>> StockCheck([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Ok(new List<StockCheckDto>());

            var matchingAllergens = await _context.AllergenCodes
                .Where(a => a.CodePrick.Contains(query) ||
                            (a.DescriptionGreek != null && a.DescriptionGreek.Contains(query)) ||
                            (a.Description != null && a.Description.Contains(query)))
                .ToListAsync();

            var codes = matchingAllergens.Select(a => a.CodePrick).ToList();

            var stockRaw = await _context.StockReceipts
                .Where(r => !r.IsDepleted && codes.Contains(r.CodePrick))
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();

            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);
            var allergenLookup = matchingAllergens.ToDictionary(a => a.CodePrick);

            var result = stockRaw.Select(s =>
            {
                allergenLookup.TryGetValue(s.CodePrick, out var allergen);
                productLookup.TryGetValue(s.ProductTypeCode, out var product);
                return new StockCheckDto
                {
                    CodePrick = s.CodePrick,
                    Description = allergen?.DescriptionGreek ?? allergen?.Description,
                    ProductTypeCode = s.ProductTypeCode,
                    ProductDescription = product?.Description,
                    Company = allergen?.Company,
                    TotalRemaining = s.Total
                };
            })
            .OrderBy(x => x.CodePrick)
            .ToList();

            return Ok(result);
        }

        // ---- Λίστα Production Orders (με τις γραμμές τους) ----
        [HttpGet("orders")]
        public async Task<ActionResult<List<ProductionOrderDto>>> GetOrders([FromQuery] string? company, [FromQuery] string? status)
        {
            var query = _context.ProductionOrders.AsQueryable();
            if (!string.IsNullOrEmpty(company))
                query = query.Where(o => o.Company == company);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);
            else
                query = query.Where(o => o.Status == "Open" || o.Status == "PartiallyReceived");

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            var orderIds = orders.Select(o => o.ProductionOrderID).ToList();

            var lines = await _context.ProductionOrderLines
                .Where(l => orderIds.Contains(l.ProductionOrderID))
                .ToListAsync();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            var result = orders.Select(o => new ProductionOrderDto
            {
                ProductionOrderID = o.ProductionOrderID,
                ProductionOrderCode = o.ProductionOrderCode,
                Company = o.Company,
                OrderDate = o.OrderDate,
                ExpectedDate = o.ExpectedDate,
                Status = o.Status,
                Notes = o.Notes,
                Lines = lines.Where(l => l.ProductionOrderID == o.ProductionOrderID).Select(l =>
                {
                    allergenLookup.TryGetValue(l.CodePrick, out var allergen);
                    productLookup.TryGetValue(l.ProductTypeCode, out var product);
                    return new ProductionOrderLineDto
                    {
                        ProductionOrderLineID = l.ProductionOrderLineID,
                        CodePrick = l.CodePrick,
                        AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                        ProductTypeCode = l.ProductTypeCode,
                        ProductDescription = product?.Description,
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        LineStatus = l.LineStatus
                    };
                }).ToList()
            }).ToList();

            return Ok(result);
        }

        // ---- Dropdown: κωδικοί αλλεργιογόνων ανά εταιρεία ----
        [HttpGet("allergens")]
        public async Task<ActionResult<List<SimpleCodeOptionDto>>> GetAllergens([FromQuery] string? company)
        {
            var query = _context.AllergenCodes.Where(a => a.IsActive);
            if (!string.IsNullOrEmpty(company))
                query = query.Where(a => a.Company == company);

            var list = await query
                .OrderBy(a => a.CodePrick)
                .Select(a => new SimpleCodeOptionDto
                {
                    Code = a.CodePrick,
                    Description = a.DescriptionGreek ?? a.Description,
                    DescriptionGreek = a.DescriptionGreek,
                    DescriptionOther = a.DescriptionOther ?? a.Description,
                    Company = a.Company
                })
                .ToListAsync();
            return Ok(list);
        }

        // ---- Dropdown: τύποι προϊόντος ανά εταιρεία ----
        [HttpGet("producttypes")]
        public async Task<ActionResult<List<SimpleCodeOptionDto>>> GetProductTypes([FromQuery] string? company)
        {
            var query = _context.ProductTypes.Where(p => p.IsActive);
            if (!string.IsNullOrEmpty(company))
                query = query.Where(p => p.Company == company);

            var list = await query
                .OrderBy(p => p.ProductTypeCode)
                .Select(p => new SimpleCodeOptionDto { Code = p.ProductTypeCode, Description = p.Description, Company = p.Company })
                .ToListAsync();
            return Ok(list);
        }

        // ---- Δημιουργία νέας Production Order (header + lines) ----
        [HttpPost("orders")]
        public async Task<ActionResult<ProductionOrderDto>> CreateOrder([FromBody] CreateProductionOrderRequest req)
        {
            if (req.Lines == null || req.Lines.Count == 0)
                return BadRequest("Η παραγγελία πρέπει να έχει τουλάχιστον μία γραμμή.");

            var orderCode = await GenerateProductionOrderCode(req.Company, req.OrderDate);

            var order = new ProductionOrder
            {
                ProductionOrderCode = orderCode,
                Company = req.Company,
                OrderDate = req.OrderDate,
                ExpectedDate = req.ExpectedDate,
                Status = "Open",
                Notes = req.Notes,
                CreatedBy = req.CreatedBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.ProductionOrders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var l in req.Lines)
            {
                _context.ProductionOrderLines.Add(new ProductionOrderLine
                {
                    ProductionOrderID = order.ProductionOrderID,
                    CodePrick = l.CodePrick,
                    ProductTypeCode = l.ProductTypeCode,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityReceived = 0,
                    LineStatus = "Open",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();

            return Ok(new ProductionOrderDto
            {
                ProductionOrderID = order.ProductionOrderID,
                ProductionOrderCode = order.ProductionOrderCode,
                Company = order.Company,
                OrderDate = order.OrderDate,
                Status = order.Status
            });
        }

        // ---- Δημιουργία κωδικού production order: SM/BM-PO-YYMMDD-αύξων ----
        private async Task<string> GenerateProductionOrderCode(string company, DateTime orderDate)
        {
            var datePart = orderDate.ToString("yyMMdd");
            var prefix = $"{company}-PO-{datePart}-";

            for (int attempt = 0; attempt < 5; attempt++)
            {
                var existingCount = await _context.ProductionOrders
                    .CountAsync(o => o.ProductionOrderCode.StartsWith(prefix));

                var sequence = existingCount + 1 + attempt;
                var digits = sequence > 99 ? 3 : 2;
                var candidate = $"{prefix}{sequence.ToString().PadLeft(digits, '0')}";

                var exists = await _context.ProductionOrders.AnyAsync(o => o.ProductionOrderCode == candidate);
                if (!exists)
                    return candidate;
            }

            return $"{prefix}{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        // ---- Συγκεντρωτική προβολή: πόσο περιμένουμε συνολικά ανά κωδικό+τύπο ----
        [HttpGet("pending-summary")]
        public async Task<ActionResult<List<PendingReceiptSummaryDto>>> GetPendingSummary([FromQuery] string? company)
        {
            var linesQuery = _context.ProductionOrderLines
                .Include(l => l.ProductionOrder)
                .Where(l => l.LineStatus == "Open" || l.LineStatus == "PartiallyReceived");

            if (!string.IsNullOrEmpty(company))
                linesQuery = linesQuery.Where(l => l.ProductionOrder.Company == company);

            var lines = await linesQuery.ToListAsync();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            var result = lines
                .GroupBy(l => new { l.CodePrick, l.ProductTypeCode, l.ProductionOrder.Company })
                .Select(g =>
                {
                    allergenLookup.TryGetValue(g.Key.CodePrick, out var allergen);
                    productLookup.TryGetValue(g.Key.ProductTypeCode, out var product);
                    return new PendingReceiptSummaryDto
                    {
                        CodePrick = g.Key.CodePrick,
                        AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                        ProductTypeCode = g.Key.ProductTypeCode,
                        ProductDescription = product?.Description,
                        Company = g.Key.Company,
                        TotalPending = g.Sum(l => l.QuantityOrdered - l.QuantityReceived),
                        OrdersCount = g.Select(l => l.ProductionOrderID).Distinct().Count()
                    };
                })
                .Where(x => x.TotalPending > 0)
                .OrderByDescending(x => x.TotalPending)
                .ToList();

            return Ok(result);
        }

        // ---- Parse Excel αρχείου παραλαβής (3 στήλες: Είδος / Κωδικός / Τεμάχια) ----
        [HttpPost("parse-receiving-excel")]
        public async Task<ActionResult<List<ReceivingImportRowDto>>> ParseReceivingExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Δεν στάλθηκε αρχείο.");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            var allergenCodes = (await _context.AllergenCodes.Select(a => a.CodePrick).ToListAsync()).ToHashSet();
            var productTypeCodes = (await _context.ProductTypes.Select(p => p.ProductTypeCode).ToListAsync()).ToHashSet();

            using var wb = new ClosedXML.Excel.XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var result = new List<ReceivingImportRowDto>();

            foreach (var row in ws.RangeUsed().RowsUsed().Skip(1))
            {
                if (row.IsEmpty()) continue;

                var typeCode = row.Cell(1).Value.ToString().Trim();
                var code = row.Cell(2).Value.ToString().Trim().ToUpper();
                var qtyStr = row.Cell(3).Value.ToString().Trim();
                int.TryParse(qtyStr, out int qty);

                var dto = new ReceivingImportRowDto
                {
                    ProductTypeCode = typeCode,
                    CodePrick = code,
                    Quantity = qty
                };

                if (string.IsNullOrEmpty(typeCode))
                    dto.Error = "Λείπει Είδος";
                else if (!productTypeCodes.Contains(typeCode))
                    dto.Error = $"Άγνωστο Είδος '{typeCode}'";
                else if (string.IsNullOrEmpty(code))
                    dto.Error = "Λείπει Κωδικός";
                else if (!allergenCodes.Contains(code))
                    dto.Error = $"Άγνωστος κωδικός '{code}'";
                else if (qty <= 0)
                    dto.Error = "Μη έγκυρη ποσότητα";
                else
                    dto.Valid = true;

                result.Add(dto);
            }

            return Ok(result);
        }

        // ---- Λήψη προτύπου Excel για import νέας παραγγελίας παραγωγής ----
        [HttpGet("import/template")]
        public ActionResult DownloadOrderImportTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("Παραγγελίες Παραγωγής");
            ws.Cell(1, 1).Value = "Εταιρεία (SM/BM)";
            ws.Cell(1, 2).Value = "Τύπος (κωδικός)";
            ws.Cell(1, 3).Value = "Ημερομηνία (ΗΗ/ΜΜ/ΕΕΕΕ)";
            ws.Cell(1, 4).Value = "Κωδικός Αλλεργιογόνου";
            ws.Cell(1, 5).Value = "Ποσότητα";
            ws.Range(1, 1, 1, 5).Style.Font.SetBold();

            ws.Cell(2, 1).Value = "BM";
            ws.Cell(2, 2).Value = "91";
            ws.Cell(2, 3).Value = DateTime.Today;
            ws.Cell(2, 4).Value = "A-001";
            ws.Cell(2, 5).Value = 20;
            ws.Cell(3, 1).Value = "BM";
            ws.Cell(3, 2).Value = "91";
            ws.Cell(3, 3).Value = DateTime.Today;
            ws.Cell(3, 4).Value = "F-042";
            ws.Cell(3, 5).Value = 10;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Πρότυπο_Εισαγωγής_Παραγγελιών_Παραγωγής.xlsx");
        }

        // ---- Προεπισκόπηση import νέας παραγγελίας παραγωγής (χωρίς καταχώρηση) ----
        [HttpPost("import/preview")]
        public async Task<ActionResult<ImportProductionPreviewResult>> ImportOrderPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Δεν στάλθηκε αρχείο.");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var ws = workbook.Worksheets.First();
            var rows = ws.RangeUsed().RowsUsed().Skip(1);

            var allergens = await _context.AllergenCodes.ToListAsync();
            var productTypes = await _context.ProductTypes.ToListAsync();

            var groupsDict = new Dictionary<string, ImportProductionOrderGroupPreview>();
            int totalRows = 0, errorRows = 0;

            foreach (var row in rows)
            {
                if (row.IsEmpty()) continue;
                totalRows++;

                var company = row.Cell(1).GetString().Trim().ToUpper();
                var productType = row.Cell(2).GetString().Trim();
                DateTime orderDate;
                try { orderDate = row.Cell(3).GetDateTime(); }
                catch { orderDate = DateTime.Today; }
                var code = row.Cell(4).GetString().Trim().ToUpper();
                int.TryParse(row.Cell(5).GetString().Trim(), out int quantity);

                var key = $"{company}|{productType}|{orderDate:yyyyMMdd}";
                if (!groupsDict.TryGetValue(key, out var group))
                {
                    var matchedType = productTypes.FirstOrDefault(p => p.ProductTypeCode == productType);

                    group = new ImportProductionOrderGroupPreview
                    {
                        Company = company,
                        ProductTypeCode = productType,
                        ProductTypeValid = matchedType != null,
                        OrderDate = orderDate
                    };
                    if (!group.ProductTypeValid)
                        group.Warnings.Add("Άγνωστος τύπος προϊόντος");

                    groupsDict[key] = group;
                }

                var matchedAllergen = allergens.FirstOrDefault(a => a.CodePrick.Equals(code, StringComparison.OrdinalIgnoreCase))
                    ?? allergens.FirstOrDefault(a => (a.DescriptionGreek ?? "").Equals(code, StringComparison.OrdinalIgnoreCase));

                var lineValid = matchedAllergen != null && quantity > 0;
                if (!lineValid) errorRows++;

                group.Lines.Add(new ImportProductionOrderLinePreview
                {
                    CodePrick = matchedAllergen?.CodePrick ?? code,
                    AllergenDescription = matchedAllergen?.DescriptionGreek ?? matchedAllergen?.Description ?? "⚠ Άγνωστος κωδικός",
                    Quantity = quantity,
                    CodeValid = lineValid
                });
            }

            foreach (var g in groupsDict.Values)
                g.HasErrors = !g.ProductTypeValid || !g.Lines.Any(l => l.CodeValid);

            return Ok(new ImportProductionPreviewResult
            {
                Groups = groupsDict.Values.ToList(),
                TotalRows = totalRows,
                ErrorRows = errorRows
            });
        }

        // ---- Επιβεβαίωση import (πραγματική δημιουργία Production Orders) ----
        [HttpPost("import/commit")]
        public async Task<ActionResult<int>> ImportOrderCommit([FromBody] CommitProductionImportRequest req)
        {
            int created = 0;

            foreach (var g in req.Groups.Where(g => !g.HasErrors))
            {
                var orderCode = await GenerateProductionOrderCode(g.Company, g.OrderDate);
                var order = new ProductionOrder
                {
                    ProductionOrderCode = orderCode,
                    Company = g.Company,
                    OrderDate = g.OrderDate,
                    Status = "Open",
                    CreatedBy = req.CreatedBy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.ProductionOrders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var l in g.Lines.Where(l => l.CodeValid))
                {
                    _context.ProductionOrderLines.Add(new ProductionOrderLine
                    {
                        ProductionOrderID = order.ProductionOrderID,
                        CodePrick = l.CodePrick,
                        ProductTypeCode = g.ProductTypeCode,
                        QuantityOrdered = l.Quantity,
                        QuantityReceived = 0,
                        LineStatus = "Open",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();
                created++;
            }

            return Ok(created);
        }

        // ---- Καταχώρηση παραλαβής (καλεί sp_ReceiveProduction) ----
        [HttpPost("receive")]
        public async Task<ActionResult<ReceiveStockResult>> ReceiveStock([FromBody] ReceiveStockRequest req)
        {
            var connection = (MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "sp_ReceiveProduction";
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new MySqlParameter("p_CodePrick", MySqlDbType.VarChar) { Value = req.CodePrick });
            cmd.Parameters.Add(new MySqlParameter("p_ProductTypeCode", MySqlDbType.VarChar) { Value = req.ProductTypeCode });
            cmd.Parameters.Add(new MySqlParameter("p_ReceivedDate", MySqlDbType.Date) { Value = req.ReceivedDate });
            cmd.Parameters.Add(new MySqlParameter("p_Quantity", MySqlDbType.Int32) { Value = req.Quantity });
            cmd.Parameters.Add(new MySqlParameter("p_UserID", MySqlDbType.Int32) { Value = (object?)req.CreatedBy ?? DBNull.Value });
            cmd.Parameters.Add(new MySqlParameter("p_Notes", MySqlDbType.VarChar) { Value = (object?)req.Notes ?? DBNull.Value });

            var pReceiptId = new MySqlParameter("p_ReceiptID", MySqlDbType.Int64) { Direction = ParameterDirection.Output };
            var pApplied = new MySqlParameter("p_QuantityAppliedToOrders", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
            var pExcess = new MySqlParameter("p_QuantityExcess", MySqlDbType.Int32) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(pReceiptId);
            cmd.Parameters.Add(pApplied);
            cmd.Parameters.Add(pExcess);

            await cmd.ExecuteNonQueryAsync();

            var result = new ReceiveStockResult
            {
                ReceiptID = Convert.ToInt64(pReceiptId.Value),
                QuantityAppliedToOrders = Convert.ToInt32(pApplied.Value),
                QuantityExcess = Convert.ToInt32(pExcess.Value)
            };

            return Ok(result);
        }
    }
}
