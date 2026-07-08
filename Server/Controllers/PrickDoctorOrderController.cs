using ClosedXML.Excel;
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
    public class PrickDoctorOrderController : ControllerBase
    {
        private readonly StallmedContext _context;

        public PrickDoctorOrderController(StallmedContext context)
        {
            _context = context;
        }

        // ---- Λίστα Doctor Orders (με τις γραμμές τους) ----
        [HttpGet("orders")]
        public async Task<ActionResult<List<DoctorOrderViewDto>>> GetOrders(
            [FromQuery] string? company, [FromQuery] int? doctorId, [FromQuery] string? status)
        {
            var query = _context.DoctorOrders.Include(o => o.Doctor).AsQueryable();
            if (!string.IsNullOrEmpty(company))
                query = query.Where(o => o.Company == company);
            if (doctorId.HasValue)
                query = query.Where(o => o.DoctorID == doctorId.Value);
            if (string.IsNullOrEmpty(status) || status == "Open")
                query = query.Where(o => o.OrderStatus == "Open");
            else if (status != "All")
                query = query.Where(o => o.OrderStatus == status);
            // status == "All" -> καμία φίλτρανση, δείχνει όλες τις καταστάσεις

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();
            var orderIds = orders.Select(o => o.OrderID).ToList();

            var lines = await _context.DoctorOrderLines
                .Where(l => orderIds.Contains(l.OrderID))
                .ToListAsync();
            var lineIds = lines.Select(l => l.OrderLineID).ToList();

            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            // Διαθέσιμο stock ανά κωδικό+τύπο (μόνο ελεύθερο, μη δεσμευμένο)
            var stockAgg = await _context.StockReceipts
                .Where(r => !r.IsDepleted)
                .GroupBy(r => new { r.CodePrick, r.ProductTypeCode })
                .Select(g => new { g.Key.CodePrick, g.Key.ProductTypeCode, Total = g.Sum(x => x.QuantityRemaining) })
                .ToListAsync();
            var stockDict = stockAgg.ToDictionary(x => (x.CodePrick, x.ProductTypeCode), x => x.Total);

            // "Δεσμευμένο αλλού": ενεργές δεσμεύσεις σε ΑΛΛΕΣ γραμμές, ίδιου κωδικού+τύπου
            var activeAllocs = await _context.OrderAllocations
                .Where(a => a.AllocationStatus == "Active")
                .ToListAsync();
            var allLineCodes = await _context.DoctorOrderLines
                .Select(l => new { l.OrderLineID, l.CodePrick, l.ProductTypeCode })
                .ToListAsync();
            var lineCodeLookup = allLineCodes.ToDictionary(l => l.OrderLineID, l => (l.CodePrick, l.ProductTypeCode));

            int ElsewhereFor(long thisLineId, string codePrick, string productTypeCode)
            {
                return activeAllocs
                    .Where(a => a.OrderLineID != thisLineId &&
                                lineCodeLookup.TryGetValue(a.OrderLineID, out var cc) &&
                                cc.CodePrick == codePrick && cc.ProductTypeCode == productTypeCode)
                    .Sum(a => a.QuantityAllocated);
            }

            var attachmentCounts = await _context.DoctorOrderAttachments
                .Where(a => orderIds.Contains(a.OrderID))
                .GroupBy(a => a.OrderID)
                .Select(g => new { OrderID = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OrderID, x => x.Count);

            var result = orders.Select(o => new DoctorOrderViewDto
            {
                OrderID = o.OrderID,
                OrderCode = o.OrderCode,
                DoctorName = o.Doctor != null ? o.Doctor.FullName : o.DoctorName,
                Company = o.Company,
                OrderDate = o.OrderDate,
                OrderStatus = o.OrderStatus,
                ShippedAt = o.ShippedAt,
                CourierTrackingCode = o.CourierTrackingCode,
                RecipientName = o.RecipientName,
                ShippingAddress = o.ShippingAddress,
                ShippingCity = o.ShippingCity,
                ShippingPostalCode = o.ShippingPostalCode,
                ShippingPhone = o.ShippingPhone,
                Notes = o.Notes,
                AttachmentCount = attachmentCounts.TryGetValue(o.OrderID, out var cnt) ? cnt : 0,
                Lines = lines.Where(l => l.OrderID == o.OrderID).Select(l =>
                {
                    allergenLookup.TryGetValue(l.CodePrick, out var allergen);
                    productLookup.TryGetValue(l.ProductTypeCode, out var product);
                    stockDict.TryGetValue((l.CodePrick, l.ProductTypeCode), out var available);
                    return new DoctorOrderLineViewDto
                    {
                        OrderLineID = l.OrderLineID,
                        CodePrick = l.CodePrick,
                        AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                        ProductTypeCode = l.ProductTypeCode,
                        ProductDescription = product?.Description,
                        QuantityRequested = l.QuantityRequested,
                        QuantityAllocated = l.QuantityAllocated,
                        QuantityCancelled = l.QuantityCancelled,
                        LineStatus = l.LineStatus,
                        AvailableStock = available,
                        ElsewhereQuantity = ElsewhereFor(l.OrderLineID, l.CodePrick, l.ProductTypeCode)
                    };
                }).ToList()
            }).ToList();

            return Ok(result);
        }

        // ---- Αναζήτηση γιατρών (για το dropdown/search στη φόρμα) ----
        [HttpGet("doctors")]
        public async Task<ActionResult<List<DoctorOptionDto>>> SearchDoctors([FromQuery] string? search)
        {
            var query = _context.Doctors.Where(d => d.IsActive);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(d => d.FullName.Contains(search));

            var list = await query.OrderBy(d => d.FullName).Take(50)
                .Select(d => new DoctorOptionDto { DoctorID = d.DoctorID, FullName = d.FullName })
                .ToListAsync();
            return Ok(list);
        }

        // ---- Γρήγορη προσθήκη νέου γιατρού (χωρίς να φύγεις από τη φόρμα) ----
        [HttpPost("doctors/quickadd")]
        public async Task<ActionResult<DoctorOptionDto>> QuickAddDoctor([FromBody] QuickAddDoctorRequest req)
        {
            var doctor = new Doctor
            {
                FullName = req.FullName,
                Phone = req.Phone,
                City = req.City,
                Email = req.Email,
                IsActive = true,
                CreatedBy = req.CreatedBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();

            return Ok(new DoctorOptionDto { DoctorID = doctor.DoctorID, FullName = doctor.FullName });
        }

        // ---- Δημιουργία νέας παραγγελίας γιατρού (header + lines) ----
        [HttpPost("orders")]
        public async Task<ActionResult<DoctorOrderViewDto>> CreateOrder([FromBody] CreateDoctorOrderRequest req)
        {
            if (req.Lines == null || req.Lines.Count == 0)
                return BadRequest("Η παραγγελία πρέπει να έχει τουλάχιστον μία γραμμή.");
            if (req.DoctorID == null)
                return BadRequest("Επίλεξε γιατρό.");

            var orderCode = await GenerateOrderCode(req.Company, req.OrderDate);

            var order = new DoctorOrder
            {
                OrderCode = orderCode,
                DoctorID = req.DoctorID,
                Company = req.Company,
                OrderDate = req.OrderDate,
                OrderStatus = "Open",
                Notes = req.Notes,
                RecipientName = req.RecipientName,
                ShippingAddress = req.ShippingAddress,
                ShippingCity = req.ShippingCity,
                ShippingPostalCode = req.ShippingPostalCode,
                ShippingPhone = req.ShippingPhone,
                CreatedBy = req.CreatedBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.DoctorOrders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var l in req.Lines)
            {
                _context.DoctorOrderLines.Add(new DoctorOrderLine
                {
                    OrderID = order.OrderID,
                    CodePrick = l.CodePrick,
                    ProductTypeCode = l.ProductTypeCode,
                    QuantityRequested = l.QuantityRequested,
                    QuantityAllocated = 0,
                    QuantityCancelled = 0,
                    LineStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
            await _context.SaveChangesAsync();

            return Ok(new DoctorOrderViewDto
            {
                OrderID = order.OrderID,
                OrderCode = order.OrderCode,
                Company = order.Company,
                OrderDate = order.OrderDate,
                OrderStatus = order.OrderStatus
            });
        }

        // =====================================================================
        // "Δεσμευμένο αλλού" + Κλέψιμο
        // =====================================================================

        // ---- Λίστα ενεργών δεσμεύσεων ίδιου κωδικού+τύπου σε ΑΛΛΕΣ γραμμές ----
        [HttpGet("elsewhere")]
        public async Task<ActionResult<List<ElsewhereAllocationDto>>> GetElsewhere(
            [FromQuery] string codePrick, [FromQuery] string productTypeCode, [FromQuery] long excludeOrderLineId)
        {
            var otherLineIds = await _context.DoctorOrderLines
                .Where(l => l.CodePrick == codePrick && l.ProductTypeCode == productTypeCode && l.OrderLineID != excludeOrderLineId)
                .Select(l => l.OrderLineID)
                .ToListAsync();

            var allocs = await _context.OrderAllocations
                .Where(a => a.AllocationStatus == "Active" && otherLineIds.Contains(a.OrderLineID))
                .OrderBy(a => a.AllocationDate)
                .ToListAsync();

            var lineOrderMap = await _context.DoctorOrderLines
                .Where(l => otherLineIds.Contains(l.OrderLineID))
                .Select(l => new { l.OrderLineID, l.OrderID })
                .ToListAsync();
            var orderIds = lineOrderMap.Select(x => x.OrderID).Distinct().ToList();
            var ordersInfo = await _context.DoctorOrders.Include(o => o.Doctor)
                .Where(o => orderIds.Contains(o.OrderID)).ToListAsync();

            var result = allocs.Select(a =>
            {
                var lineOrder = lineOrderMap.First(x => x.OrderLineID == a.OrderLineID);
                var ord = ordersInfo.First(o => o.OrderID == lineOrder.OrderID);
                return new ElsewhereAllocationDto
                {
                    AllocationID = a.AllocationID,
                    OrderLineID = a.OrderLineID,
                    OrderCode = ord.OrderCode,
                    DoctorName = ord.Doctor != null ? ord.Doctor.FullName : ord.DoctorName,
                    QuantityAllocated = a.QuantityAllocated,
                    AllocationDate = a.AllocationDate
                };
            }).ToList();

            return Ok(result);
        }

        // ---- Κλέψιμο: reverse του source, allocate στο target, re-allocate το υπόλοιπο πίσω ----
        [HttpPost("steal")]
        public async Task<ActionResult<StealResult>> Steal([FromBody] StealRequest req)
        {
            var connection = (MySqlConnector.MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var alloc = await _context.OrderAllocations.FindAsync(req.SourceAllocationID);
            if (alloc == null || alloc.AllocationStatus != "Active")
                return BadRequest(new StealResult { Success = false, Message = "Η δέσμευση δεν είναι πλέον ενεργή." });

            if (req.Quantity <= 0 || req.Quantity > alloc.QuantityAllocated)
                return BadRequest(new StealResult { Success = false, Message = "Μη έγκυρη ποσότητα." });

            var sourceLineId = alloc.OrderLineID;
            var originalQuantity = alloc.QuantityAllocated;

            using (var cmd1 = connection.CreateCommand())
            {
                cmd1.CommandText = "sp_ReverseAllocation";
                cmd1.CommandType = System.Data.CommandType.StoredProcedure;
                cmd1.Parameters.Add(new MySqlConnector.MySqlParameter("p_AllocationID", MySqlConnector.MySqlDbType.Int64) { Value = req.SourceAllocationID });
                cmd1.Parameters.Add(new MySqlConnector.MySqlParameter("p_UserID", MySqlConnector.MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
                cmd1.Parameters.Add(new MySqlConnector.MySqlParameter("p_Reason", MySqlConnector.MySqlDbType.VarChar) { Value = "Μετακίνηση (κλέψιμο) σε άλλη παραγγελία" });
                await cmd1.ExecuteNonQueryAsync();
            }

            using (var cmd2 = connection.CreateCommand())
            {
                cmd2.CommandText = "sp_AllocateStock";
                cmd2.CommandType = System.Data.CommandType.StoredProcedure;
                cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("p_OrderLineID", MySqlConnector.MySqlDbType.Int64) { Value = req.TargetOrderLineID });
                cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("p_QuantityToAllocate", MySqlConnector.MySqlDbType.Int32) { Value = req.Quantity });
                cmd2.Parameters.Add(new MySqlConnector.MySqlParameter("p_UserID", MySqlConnector.MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
                var outAlloc = new MySqlConnector.MySqlParameter("p_QuantityActuallyAllocated", MySqlConnector.MySqlDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
                cmd2.Parameters.Add(outAlloc);
                await cmd2.ExecuteNonQueryAsync();
            }

            var remainder = originalQuantity - req.Quantity;
            if (remainder > 0)
            {
                using var cmd3 = connection.CreateCommand();
                cmd3.CommandText = "sp_AllocateStock";
                cmd3.CommandType = System.Data.CommandType.StoredProcedure;
                cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("p_OrderLineID", MySqlConnector.MySqlDbType.Int64) { Value = sourceLineId });
                cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("p_QuantityToAllocate", MySqlConnector.MySqlDbType.Int32) { Value = remainder });
                cmd3.Parameters.Add(new MySqlConnector.MySqlParameter("p_UserID", MySqlConnector.MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
                var outAlloc2 = new MySqlConnector.MySqlParameter("p_QuantityActuallyAllocated", MySqlConnector.MySqlDbType.Int32) { Direction = System.Data.ParameterDirection.Output };
                cmd3.Parameters.Add(outAlloc2);
                await cmd3.ExecuteNonQueryAsync();
            }

            return Ok(new StealResult { Success = true });
        }

        // =====================================================================
        // Ακύρωση γραμμής
        // =====================================================================
        [HttpPost("cancel-line")]
        public async Task<ActionResult> CancelLine([FromBody] CancelLineRequest req)
        {
            var line = await _context.DoctorOrderLines.FindAsync(req.OrderLineID);
            if (line == null) return NotFound();

            var pending = line.QuantityRequested - line.QuantityAllocated - line.QuantityCancelled;
            if (pending <= 0)
                return BadRequest("Δεν υπάρχει εκκρεμές υπόλοιπο σε αυτή τη γραμμή για ακύρωση.");

            line.QuantityCancelled += pending;
            if (line.QuantityAllocated == 0)
                line.LineStatus = "Cancelled";
            else
                line.LineStatus = "Fulfilled";
            line.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            var order = await _context.DoctorOrders.FindAsync(line.OrderID);
            var siblingLines = await _context.DoctorOrderLines.Where(l => l.OrderID == line.OrderID).ToListAsync();
            if (order != null && order.OrderStatus == "Open" &&
                siblingLines.All(l => l.LineStatus == "Fulfilled" || l.LineStatus == "Cancelled"))
            {
                order.OrderStatus = "ReadyToShip";
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // =====================================================================
        // Επεξεργασία παραγγελίας: notes, προσθήκη/αφαίρεση γραμμής
        // =====================================================================
        [HttpPost("update-notes")]
        public async Task<ActionResult> UpdateNotes([FromBody] UpdateNotesRequest req)
        {
            var order = await _context.DoctorOrders.FindAsync(req.OrderID);
            if (order == null) return NotFound();
            order.Notes = req.Notes;
            order.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("add-line")]
        public async Task<ActionResult> AddLine([FromBody] AddOrderLineRequest req)
        {
            var order = await _context.DoctorOrders.FindAsync(req.OrderID);
            if (order == null) return NotFound();
            if (req.Quantity <= 0) return BadRequest("Μη έγκυρη ποσότητα.");

            var existingLine = await _context.DoctorOrderLines.FirstOrDefaultAsync(l => l.OrderID == req.OrderID);
            var productTypeCode = existingLine?.ProductTypeCode ?? "";

            _context.DoctorOrderLines.Add(new DoctorOrderLine
            {
                OrderID = req.OrderID,
                CodePrick = req.CodePrick,
                ProductTypeCode = productTypeCode,
                QuantityRequested = req.Quantity,
                QuantityAllocated = 0,
                QuantityCancelled = 0,
                LineStatus = "Pending",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

            if (order.OrderStatus == "ReadyToShip")
                order.OrderStatus = "Open";
            order.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("remove-line")]
        public async Task<ActionResult> RemoveLine([FromBody] RemoveOrderLineRequest req)
        {
            var line = await _context.DoctorOrderLines.FindAsync(req.OrderLineID);
            if (line == null) return NotFound();
            if (line.QuantityAllocated > 0)
                return BadRequest("Δεν μπορείς να αφαιρέσεις γραμμή που έχει ήδη δεσμευμένο stock - ανακάλεσε πρώτα τη δέσμευση.");

            _context.DoctorOrderLines.Remove(line);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // =====================================================================
        // Attachments (εικόνες)
        // =====================================================================
        [HttpGet("attachments/{orderId}")]
        public async Task<ActionResult<List<AttachmentDto>>> GetAttachments(long orderId)
        {
            var list = await _context.DoctorOrderAttachments
                .Where(a => a.OrderID == orderId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AttachmentDto { AttachmentID = a.AttachmentID, FileName = a.FileName, CreatedAt = a.CreatedAt })
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost("attachments/{orderId}")]
        public async Task<ActionResult> UploadAttachment(long orderId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Δεν στάλθηκε αρχείο.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            _context.DoctorOrderAttachments.Add(new DoctorOrderAttachment
            {
                OrderID = orderId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                ImageData = ms.ToArray(),
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("attachments/image/{attachmentId}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetAttachmentImage(long attachmentId)
        {
            var att = await _context.DoctorOrderAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            return File(att.ImageData, att.ContentType ?? "application/octet-stream");
        }

        [HttpPost("attachments/delete/{attachmentId}")]
        public async Task<ActionResult> DeleteAttachment(long attachmentId)
        {
            var att = await _context.DoctorOrderAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            _context.DoctorOrderAttachments.Remove(att);
            await _context.SaveChangesAsync();
            return Ok();
        }


        // ---- Λήψη προτύπου Excel για import ----
        [HttpGet("import/template")]
        public ActionResult DownloadTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Παραγγελίες");
            ws.Cell(1, 1).Value = "Εταιρεία (SM/BM)";
            ws.Cell(1, 2).Value = "Γιατρός (Ονοματεπώνυμο)";
            ws.Cell(1, 3).Value = "Τύπος (κωδικός)";
            ws.Cell(1, 4).Value = "Ημερομηνία (ΗΗ/ΜΜ/ΕΕΕΕ)";
            ws.Cell(1, 5).Value = "Κωδικός Αλλεργιογόνου";
            ws.Cell(1, 6).Value = "Ποσότητα";
            ws.Range(1, 1, 1, 6).Style.Font.SetBold();

            // Παράδειγμα γραμμής
            ws.Cell(2, 1).Value = "BM";
            ws.Cell(2, 2).Value = "Παπαδόπουλος Γιώργος";
            ws.Cell(2, 3).Value = "91";
            ws.Cell(2, 4).Value = DateTime.Today;
            ws.Cell(2, 5).Value = "A-001";
            ws.Cell(2, 6).Value = 2;
            ws.Cell(3, 1).Value = "BM";
            ws.Cell(3, 2).Value = "Παπαδόπουλος Γιώργος";
            ws.Cell(3, 3).Value = "91";
            ws.Cell(3, 4).Value = DateTime.Today;
            ws.Cell(3, 5).Value = "F-042";
            ws.Cell(3, 6).Value = 1;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Πρότυπο_Εισαγωγής_Παραγγελιών.xlsx");
        }

        // ---- Προεπισκόπηση import (χωρίς καταχώρηση) ----
        [HttpPost("import/preview")]
        public async Task<ActionResult<ImportPreviewResult>> ImportPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Δεν στάλθηκε αρχείο.");

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.First();
            var rows = ws.RangeUsed().RowsUsed().Skip(1); // παράλειψη επικεφαλίδων

            var allergens = await _context.AllergenCodes.ToListAsync();
            var productTypes = await _context.ProductTypes.ToListAsync();
            var doctors = await _context.Doctors.Where(d => d.IsActive).ToListAsync();

            var groupsDict = new Dictionary<string, ImportOrderGroupPreview>();
            int totalRows = 0, errorRows = 0;

            foreach (var row in rows)
            {
                if (row.IsEmpty()) continue;
                totalRows++;

                var company = row.Cell(1).GetString().Trim().ToUpper();
                var doctorName = row.Cell(2).GetString().Trim();
                var productType = row.Cell(3).GetString().Trim();
                DateTime orderDate;
                try { orderDate = row.Cell(4).GetDateTime(); }
                catch { orderDate = DateTime.Today; }
                var code = row.Cell(5).GetString().Trim().ToUpper();
                int.TryParse(row.Cell(6).GetString().Trim(), out int quantity);

                var key = $"{company}|{doctorName}|{productType}|{orderDate:yyyyMMdd}";
                if (!groupsDict.TryGetValue(key, out var group))
                {
                    var matchedDoctor = doctors.FirstOrDefault(d => d.FullName.Equals(doctorName, StringComparison.OrdinalIgnoreCase));
                    var matchedType = productTypes.FirstOrDefault(p => p.ProductTypeCode == productType);

                    group = new ImportOrderGroupPreview
                    {
                        Company = company,
                        DoctorNameRaw = doctorName,
                        MatchedDoctorId = matchedDoctor?.DoctorID,
                        IsNewDoctor = matchedDoctor == null,
                        ProductTypeCode = productType,
                        ProductTypeValid = matchedType != null,
                        OrderDate = orderDate
                    };
                    if (!group.ProductTypeValid)
                        group.Warnings.Add("Άγνωστος τύπος προϊόντος");
                    if (group.IsNewDoctor)
                        group.Warnings.Add("Νέος γιατρός θα δημιουργηθεί");

                    groupsDict[key] = group;
                }

                var matchedAllergen = allergens.FirstOrDefault(a => a.CodePrick.Equals(code, StringComparison.OrdinalIgnoreCase))
                    ?? allergens.FirstOrDefault(a => (a.DescriptionGreek ?? "").Equals(code, StringComparison.OrdinalIgnoreCase));

                var lineValid = matchedAllergen != null && quantity > 0;
                if (!lineValid) errorRows++;

                group.Lines.Add(new ImportOrderLinePreview
                {
                    CodePrick = matchedAllergen?.CodePrick ?? code,
                    AllergenDescription = matchedAllergen?.DescriptionGreek ?? matchedAllergen?.Description ?? "⚠ Άγνωστος κωδικός",
                    Quantity = quantity,
                    CodeValid = matchedAllergen != null && quantity > 0
                });
            }

            foreach (var g in groupsDict.Values)
                g.HasErrors = !g.ProductTypeValid || g.Lines.Any(l => !l.CodeValid);

            return Ok(new ImportPreviewResult
            {
                Groups = groupsDict.Values.ToList(),
                TotalRows = totalRows,
                ErrorRows = errorRows
            });
        }

        // ---- Επιβεβαίωση import (πραγματική καταχώρηση) ----
        [HttpPost("import/commit")]
        public async Task<ActionResult<int>> ImportCommit([FromBody] CommitImportRequest req)
        {
            int created = 0;

            foreach (var g in req.Groups.Where(g => !g.HasErrors))
            {
                int doctorId;
                if (g.MatchedDoctorId.HasValue)
                {
                    doctorId = g.MatchedDoctorId.Value;
                }
                else
                {
                    var newDoctor = new Doctor
                    {
                        FullName = g.DoctorNameRaw,
                        IsActive = true,
                        CreatedBy = req.CreatedBy,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.Doctors.Add(newDoctor);
                    await _context.SaveChangesAsync();
                    doctorId = newDoctor.DoctorID;
                }

                var orderCode = await GenerateOrderCode(g.Company, g.OrderDate);
                var order = new DoctorOrder
                {
                    OrderCode = orderCode,
                    DoctorID = doctorId,
                    Company = g.Company,
                    OrderDate = g.OrderDate,
                    OrderStatus = "Open",
                    CreatedBy = req.CreatedBy,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.DoctorOrders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var l in g.Lines)
                {
                    _context.DoctorOrderLines.Add(new DoctorOrderLine
                    {
                        OrderID = order.OrderID,
                        CodePrick = l.CodePrick,
                        ProductTypeCode = g.ProductTypeCode,
                        QuantityRequested = l.Quantity,
                        QuantityAllocated = 0,
                        QuantityCancelled = 0,
                        LineStatus = "Pending",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();
                created++;
            }

            return Ok(created);
        }

        // ---- ForceComplete: διορθωτικές καταχωρήσεις (π.χ. ατελές migration) ----
        // Η παραγγελία στην πραγματικότητα έχει ήδη σταλεί - κλειδώνει απευθείας σε
        // Fulfilled + ShippedAt, ΧΩΡΙΣ να πειράξει το πραγματικό stock/ledger.
        [HttpPost("force-complete")]
        public async Task<ActionResult<ShipResult>> ForceComplete([FromBody] ShipOrderRequest req)
        {
            var order = await _context.DoctorOrders.FindAsync(req.OrderID);
            if (order == null)
                return NotFound();

            var lines = await _context.DoctorOrderLines.Where(l => l.OrderID == req.OrderID).ToListAsync();
            foreach (var line in lines)
            {
                line.QuantityAllocated = line.QuantityRequested - line.QuantityCancelled;
                line.LineStatus = "Fulfilled";
                line.UpdatedAt = DateTime.Now;
            }
            order.OrderStatus = "Fulfilled";
            order.ShippedAt = DateTime.Now;
            order.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(new ShipResult { Success = true });
        }

        // ---- Διαχωρισμός: στέλνει μόνο ό,τι έχει ήδη δεσμευτεί ----
        // Η αρχική παραγγελία "κλειδώνει" σε ό,τι δεσμεύτηκε και παίρνει status
        // ReadyToShip (όχι Fulfilled/ShippedAt ακόμα - αυτό θα γίνεται από τη
        // μελλοντική σελίδα διαχείρισης αποστολών). Το εκκρεμές υπόλοιπο πάει σε
        // ΝΕΑ παραγγελία. Κωδικοί: πρώτος διαχωρισμός -> -A/-B, επόμενος -> -C, κ.ο.κ.
        [HttpPost("split-pending")]
        public async Task<ActionResult<ShipResult>> SplitPending([FromBody] ShipOrderRequest req)
        {
            var order = await _context.DoctorOrders.FindAsync(req.OrderID);
            if (order == null)
                return NotFound();

            var lines = await _context.DoctorOrderLines.Where(l => l.OrderID == req.OrderID).ToListAsync();
            var pendingLines = lines
                .Where(l => (l.QuantityRequested - l.QuantityAllocated - l.QuantityCancelled) > 0)
                .ToList();

            if (pendingLines.Count == 0)
                return BadRequest("Δεν υπάρχει εκκρεμές υπόλοιπο για διαχωρισμό.");

            var baseCode = GetBaseOrderCode(order.OrderCode);

            if (!HasLetterSuffix(order.OrderCode))
            {
                var letterForOriginal = await GenerateNextSplitLetter(baseCode);
                order.OrderCode = $"{baseCode}-{letterForOriginal}";
            }
            var newLetter = await GenerateNextSplitLetter(baseCode);
            var newOrderCode = $"{baseCode}-{newLetter}";

            var newOrder = new DoctorOrder
            {
                OrderCode = newOrderCode,
                DoctorID = order.DoctorID,
                DoctorName = order.DoctorName,
                Company = order.Company,
                OrderDate = DateTime.Today,
                OrderStatus = "Open",
                Notes = $"Διαχωρισμός από {order.OrderCode} (εκκρεμές υπόλοιπο)",
                CreatedBy = req.UserID,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.DoctorOrders.Add(newOrder);
            await _context.SaveChangesAsync();

            foreach (var pl in pendingLines)
            {
                var pendingQty = pl.QuantityRequested - pl.QuantityAllocated - pl.QuantityCancelled;

                _context.DoctorOrderLines.Add(new DoctorOrderLine
                {
                    OrderID = newOrder.OrderID,
                    CodePrick = pl.CodePrick,
                    ProductTypeCode = pl.ProductTypeCode,
                    QuantityRequested = pendingQty,
                    QuantityAllocated = 0,
                    QuantityCancelled = 0,
                    LineStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });

                // Το υπόλοιπο της αρχικής γραμμής "κλειδώνει" στο ό,τι δεσμεύτηκε
                pl.QuantityRequested = pl.QuantityAllocated + pl.QuantityCancelled;
                pl.LineStatus = "Fulfilled";
                pl.UpdatedAt = DateTime.Now;
            }

            order.OrderStatus = "ReadyToShip";
            order.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(new ShipResult { Success = true, NewOrderCode = newOrderCode });
        }

        private static bool HasLetterSuffix(string code) =>
            code.Length >= 2 && code[^2] == '-' && char.IsUpper(code[^1]);

        private static string GetBaseOrderCode(string code) =>
            HasLetterSuffix(code) ? code[..^2] : code;

        private async Task<string> GenerateNextSplitLetter(string baseCode)
        {
            var siblings = await _context.DoctorOrders
                .Where(o => o.OrderCode == baseCode || o.OrderCode.StartsWith(baseCode + "-"))
                .Select(o => o.OrderCode)
                .ToListAsync();

            var used = new HashSet<char>();
            foreach (var c in siblings)
            {
                if (HasLetterSuffix(c))
                    used.Add(c[^1]);
            }
            var next = 'A';
            while (used.Contains(next)) next++;
            return next.ToString();
        }

        // ---- Δημιουργία κωδικού παραγγελίας: SM/BM + YYMMDD + αύξων αριθμός ημέρας ----
        // π.χ. SM260710-01, SM260710-02, BM260710-01 ...
        private async Task<string> GenerateOrderCode(string company, DateTime orderDate)
        {
            var datePart = orderDate.ToString("yyMMdd");
            var prefix = $"{company}{datePart}-";

            for (int attempt = 0; attempt < 5; attempt++)
            {
                var existingCount = await _context.DoctorOrders
                    .CountAsync(o => o.OrderCode.StartsWith(prefix));

                var sequence = existingCount + 1 + attempt; // attempt-offset αν χτυπήσει duplicate σε ταυτόχρονο request
                var digits = sequence > 99 ? 3 : 2;
                var candidate = $"{prefix}{sequence.ToString().PadLeft(digits, '0')}";

                var exists = await _context.DoctorOrders.AnyAsync(o => o.OrderCode == candidate);
                if (!exists)
                    return candidate;
            }

            // Απίθανο fallback ώστε να μην κολλήσει ποτέ η δημιουργία παραγγελίας
            return $"{prefix}{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }
    }
}
