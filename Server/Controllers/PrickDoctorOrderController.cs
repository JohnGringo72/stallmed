using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PrickDoctorOrderController : ControllerBase
    {
        private readonly StallmedContext _context;
        private readonly ILogger<PrickDoctorOrderController> _logger;

        public PrickDoctorOrderController(StallmedContext context, ILogger<PrickDoctorOrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ---- Λίστα Doctor Orders (με τις γραμμές τους) ----
        [HttpGet("orders")]
        public async Task<ActionResult<List<DoctorOrderViewDto>>> GetOrders(
            [FromQuery] string? company, [FromQuery] int? doctorId, [FromQuery] string? status)
        {
          try
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
                InvoiceType = o.InvoiceType,
                InvoiceNote = o.InvoiceNote,
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
          catch (Exception ex)
          {
              _logger.LogError(ex, "Σφάλμα στο GetOrders (company={Company}, doctorId={DoctorId}, status={Status})", company, doctorId, status);
              return StatusCode(500, "Σφάλμα φόρτωσης παραγγελιών. Δοκίμασε ξανά.");
          }
        }

        // ---- Κατάταξη γιατρών βάσει ποσοτήτων πρικ στην περίοδο ----
        [HttpGet("summary-by-doctor")]
        [Authorize(Policy = "NotWarehouse")]
        public async Task<ActionResult<List<PrickDoctorSummaryRow>>> GetSummaryByDoctor(
            [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate)
        {
            var baseRows = await _context.DoctorOrders
                .Where(o => o.OrderDate >= fromDate && o.OrderDate <= toDate && o.DoctorID != null)
                .Join(_context.Doctors, o => o.DoctorID, d => d.DoctorID,
                      (o, d) => new { d.DoctorID, d.FullName, o.Company, o.OrderID })
                .Join(_context.DoctorOrderLines.Where(l => l.LineStatus != "Cancelled"),
                      x => x.OrderID, l => l.OrderID,
                      (x, l) => new { x.DoctorID, x.FullName, x.Company, Qty = l.QuantityRequested - l.QuantityCancelled })
                .GroupBy(x => new { x.DoctorID, x.FullName })
                .Select(g => new PrickDoctorSummaryRow
                {
                    DoctorID = g.Key.DoctorID,
                    DoctorName = g.Key.FullName,
                    QtySM = g.Where(x => x.Company == "SM").Sum(x => x.Qty),
                    QtyBM = g.Where(x => x.Company == "BM").Sum(x => x.Qty),
                    QtyTotal = g.Sum(x => x.Qty)
                })
                .OrderByDescending(x => x.QtyTotal)
                .ToListAsync();

            // Σύνολα ίδιας περιόδου προηγούμενου έτους, για ένδειξη τάσης
            // (ίδια σύμβαση με το doctor-stats των εμβολίων)
            var prevFromDate = fromDate.AddYears(-1);
            var prevToDate = toDate.AddYears(-1);
            var prevTotals = (await _context.DoctorOrders
                .Where(o => o.OrderDate >= prevFromDate && o.OrderDate <= prevToDate && o.DoctorID != null)
                .Join(_context.DoctorOrderLines.Where(l => l.LineStatus != "Cancelled"),
                      o => o.OrderID, l => l.OrderID,
                      (o, l) => new { o.DoctorID, Qty = l.QuantityRequested - l.QuantityCancelled })
                .GroupBy(x => x.DoctorID)
                .Select(g => new { DoctorID = g.Key!.Value, Total = g.Sum(x => x.Qty) })
                .ToListAsync())
                .ToDictionary(x => x.DoctorID, x => x.Total);

            // Σύνολα εμβολίων ανά όνομα γιατρού από το άλλο σύστημα (WebOrders).
            // Best-effort ταύτιση με όνομα -- ΔΕΝ φιλτράρει τη λίστα, μόνο εμπλουτίζει.
            var vaccinePerName = _context.WebOrders
                .Where(x => x.Ordered >= fromDate && x.Ordered <= toDate &&
                            x.Doctor != null && x.Doctor != "" &&
                            x.TreatmentDescription != null &&
                            (x.TreatmentDescription.StartsWith("BELTA") ||
                             x.TreatmentDescription.StartsWith("STALORAL")))
                .GroupBy(x => x.Doctor)
                .Select(g => new { Doctor = g.Key!, Total = g.Sum(x => x.QNT ?? 0) })
                .AsEnumerable()
                .GroupBy(x => DoctorNameKey.Normalize(x.Doctor))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));

            foreach (var row in baseRows)
            {
                row.PrevQtyTotal = prevTotals.TryGetValue(row.DoctorID, out var p) ? p : 0;
                row.VaccineQtyTotal = vaccinePerName.TryGetValue(DoctorNameKey.Normalize(row.DoctorName), out var q)
                    ? q : (int?)null;
            }

            return Ok(baseRows);
        }

        // ---- Διαχειρίσιμη λίστα ονομάτων για αποστολή "Ίδια Μέσα" -- ΔΕΝ συνδέεται με Users ----
        [HttpGet("delivery-persons")]
        public async Task<ActionResult<List<DeliveryPersonDto>>> GetDeliveryPersons()
        {
            try
            {
                var list = await _context.DeliveryPersons
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .Select(p => new DeliveryPersonDto { PersonID = p.PersonID, Name = p.Name })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                // Δεν είναι κρίσιμο -- η φόρμα αποστολής συνεχίζει με κενή λίστα ονομάτων.
                _logger.LogError(ex, "Σφάλμα στο GetDeliveryPersons");
                return Ok(new List<DeliveryPersonDto>());
            }
        }

        [HttpPost("delivery-persons")]
        public async Task<ActionResult<DeliveryPersonDto>> AddDeliveryPerson([FromBody] AddDeliveryPersonRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest("Το όνομα είναι υποχρεωτικό.");

            var trimmed = req.Name.Trim();
            var existing = await _context.DeliveryPersons.FirstOrDefaultAsync(p => p.Name == trimmed);
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await _context.SaveChangesAsync();
                }
                return Ok(new DeliveryPersonDto { PersonID = existing.PersonID, Name = existing.Name });
            }

            var person = new DeliveryPerson { Name = trimmed, IsActive = true, CreatedAt = DateTime.Now };
            _context.DeliveryPersons.Add(person);
            await _context.SaveChangesAsync();
            return Ok(new DeliveryPersonDto { PersonID = person.PersonID, Name = person.Name });
        }

        // ---- Κλείσιμο αποστολής: ReadyToShip -> Fulfilled + στοιχεία αποστολής ----
        // Επιστρέφει πάντα 200 OK με ShipResult{Success,Message} -- ακόμα και για
        // αναμενόμενα validation failures -- ώστε ο client (μέσω DataService.Post, που
        // αγνοεί το body οποιασδήποτε μη-2xx απάντησης) να μπορεί πάντα να δει το
        // πραγματικό μήνυμα αντί να το χάνει σιωπηλά.
        [HttpPost("set-shipment")]
        public async Task<ActionResult<ShipResult>> SetShipment([FromBody] SetShipmentRequest req)
        {
            try
            {
                var order = await _context.DoctorOrders.FindAsync(req.OrderID);
                if (order == null)
                    return Ok(new ShipResult { Success = false, Message = "Η παραγγελία δεν βρέθηκε." });

                if (order.OrderStatus != "ReadyToShip")
                    return Ok(new ShipResult
                    {
                        Success = false,
                        Message = $"Η παραγγελία είναι σε κατάσταση '{order.OrderStatus}', όχι 'Προς Αποστολή' -- δεν μπορεί να κλείσει ως απεσταλμένη."
                    });

                if (string.IsNullOrWhiteSpace(req.ShippingCarrier))
                    return Ok(new ShipResult { Success = false, Message = "Λείπει ο τρόπος αποστολής." });

                // Το "Ίδια Μέσα" είναι πλέον απλή εγγραφή στο ShippingCouriers (όχι hardcoded
                // string) -- το ξεχωρίζουμε από ACS/Intralink/άλλο courier μέσω του IsOwnMeans flag.
                var isOwnMeans = await _context.ShippingCouriers
                    .AnyAsync(c => c.Name == req.ShippingCarrier && c.IsOwnMeans);

                // Το DeliveryPersonName έχει νόημα μόνο για "Ίδια Μέσα" -- για ACS/Intralink/άλλο
                // courier αγνοείται ό,τι στείλει ο client, ώστε να μη μείνει "ορφανό" σε λάθος carrier.
                var deliveryPersonName = isOwnMeans ? req.DeliveryPersonName?.Trim() : null;
                if (isOwnMeans && string.IsNullOrWhiteSpace(deliveryPersonName))
                    return Ok(new ShipResult { Success = false, Message = "Επίλεξε όνομα για αποστολή τύπου Ίδια Μέσα." });

                order.ShippingCarrier = req.ShippingCarrier;
                order.DeliveryPersonName = deliveryPersonName;
                order.OrderStatus = "Fulfilled";
                order.ShippedAt = req.ShippedDate;
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return Ok(new ShipResult { Success = true, Message = $"Η παραγγελία {order.OrderCode} απεστάλη." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Σφάλμα στο SetShipment για OrderID={OrderID}", req.OrderID);
                return Ok(new ShipResult { Success = false, Message = "Κάτι πήγε στραβά κατά την καταχώρηση αποστολής. Δοκίμασε ξανά." });
            }
        }

        // ---- Διαχειρίσιμη λίστα τρόπων αποστολής (courier) -- εκτός από το "Salesperson" ----
        [HttpGet("couriers")]
        public async Task<ActionResult<List<ShippingCourierDto>>> GetCouriers()
        {
            try
            {
                var list = await _context.ShippingCouriers
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.IsOwnMeans)
                    .ThenBy(c => c.Name)
                    .Select(c => new ShippingCourierDto { CourierID = c.CourierID, Name = c.Name, IsOwnMeans = c.IsOwnMeans })
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                // Δεν είναι κρίσιμο -- η φόρμα αποστολής συνεχίζει με μόνο το "Πωλητής".
                _logger.LogError(ex, "Σφάλμα στο GetCouriers");
                return Ok(new List<ShippingCourierDto>());
            }
        }

        [HttpPost("couriers")]
        public async Task<ActionResult<ShippingCourierDto>> AddCourier([FromBody] AddCourierRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest("Το όνομα είναι υποχρεωτικό.");

            var trimmed = req.Name.Trim();
            var existing = await _context.ShippingCouriers.FirstOrDefaultAsync(c => c.Name == trimmed);
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await _context.SaveChangesAsync();
                }
                return Ok(new ShippingCourierDto { CourierID = existing.CourierID, Name = existing.Name, IsOwnMeans = existing.IsOwnMeans });
            }

            var courier = new ShippingCourier { Name = trimmed, IsActive = true, IsOwnMeans = false, CreatedAt = DateTime.Now };
            _context.ShippingCouriers.Add(courier);
            await _context.SaveChangesAsync();
            return Ok(new ShippingCourierDto { CourierID = courier.CourierID, Name = courier.Name, IsOwnMeans = false });
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
                InvoiceType = string.IsNullOrEmpty(req.InvoiceType) ? "Κανονικό" : req.InvoiceType,
                InvoiceNote = req.InvoiceNote,
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
        // Ακύρωση / Αναίρεση ακύρωσης / Ανάκληση δέσμευσης γραμμής
        // =====================================================================

        // Κατάσταση γραμμής με βάση τις τρέχουσες ποσότητες (χρησιμοποιείται
        // από cancel-line, uncancel-line και reverse-line ώστε να μένουν συνεπή).
        private static string RecomputeLineStatus(DoctorOrderLine line)
        {
            if (line.QuantityCancelled >= line.QuantityRequested)
                return "Cancelled";
            if (line.QuantityAllocated == 0)
                return "Pending";
            if (line.QuantityAllocated < line.QuantityRequested - line.QuantityCancelled)
                return "PartiallyAllocated";
            return "Fulfilled";
        }

        // Αναπροσαρμόζει το OrderStatus με βάση τις (ήδη ενημερωμένες) γραμμές του.
        // Δεν πειράζει παραγγελίες που έχουν ήδη ολοκληρωθεί/απεσταλεί (Fulfilled).
        private async Task RecomputeOrderStatus(DoctorOrder order)
        {
            if (order.OrderStatus == "Fulfilled") return;

            var siblingLines = await _context.DoctorOrderLines.Where(l => l.OrderID == order.OrderID).ToListAsync();
            var anyUnresolved = siblingLines.Any(l => l.LineStatus == "Pending" || l.LineStatus == "PartiallyAllocated");
            var hasAnyFulfilled = siblingLines.Any(l => l.LineStatus == "Fulfilled");
            var allCancelled = siblingLines.All(l => l.LineStatus == "Cancelled");

            var newStatus = anyUnresolved ? "Open" : allCancelled ? "Cancelled" : hasAnyFulfilled ? "ReadyToShip" : order.OrderStatus;

            if (order.OrderStatus != newStatus)
            {
                order.OrderStatus = newStatus;
                order.UpdatedAt = DateTime.Now;
            }
        }

        [HttpPost("cancel-line")]
        public async Task<ActionResult> CancelLine([FromBody] CancelLineRequest req)
        {
            var line = await _context.DoctorOrderLines.FindAsync(req.OrderLineID);
            if (line == null) return NotFound();

            var pending = line.QuantityRequested - line.QuantityAllocated - line.QuantityCancelled;
            if (pending <= 0)
                return BadRequest("Δεν υπάρχει εκκρεμές υπόλοιπο σε αυτή τη γραμμή για ακύρωση.");

            line.QuantityCancelled += pending;
            line.LineStatus = RecomputeLineStatus(line);
            line.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var order = await _context.DoctorOrders.FindAsync(line.OrderID);
            if (order != null)
            {
                await RecomputeOrderStatus(order);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // ---- Αναίρεση ακύρωσης: επαναφέρει την ακυρωμένη ποσότητα σε εκκρεμές ----
        [HttpPost("uncancel-line")]
        public async Task<ActionResult> UncancelLine([FromBody] UncancelLineRequest req)
        {
            var line = await _context.DoctorOrderLines.FindAsync(req.OrderLineID);
            if (line == null) return NotFound();
            if (line.QuantityCancelled <= 0)
                return BadRequest("Δεν υπάρχει ακυρωμένη ποσότητα σε αυτή τη γραμμή για αναίρεση.");

            line.QuantityCancelled = 0;
            line.LineStatus = RecomputeLineStatus(line);
            line.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var order = await _context.DoctorOrders.FindAsync(line.OrderID);
            if (order != null)
            {
                await RecomputeOrderStatus(order);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        // ---- Ανάκληση δέσμευσης: αναιρεί ΟΛΕΣ τις ενεργές δεσμεύσεις της γραμμής ----
        [HttpPost("reverse-line")]
        public async Task<ActionResult> ReverseLine([FromBody] ReverseLineRequest req)
        {
            var line = await _context.DoctorOrderLines.FindAsync(req.OrderLineID);
            if (line == null) return NotFound();
            if (line.QuantityAllocated <= 0)
                return BadRequest("Δεν υπάρχει ενεργή δέσμευση σε αυτή τη γραμμή για ανάκληση.");

            var activeAllocs = await _context.OrderAllocations
                .Where(a => a.OrderLineID == req.OrderLineID && a.AllocationStatus == "Active")
                .ToListAsync();

            var connection = (MySqlConnector.MySqlConnection)_context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            foreach (var alloc in activeAllocs)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "sp_ReverseAllocation";
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.Add(new MySqlConnector.MySqlParameter("p_AllocationID", MySqlConnector.MySqlDbType.Int64) { Value = alloc.AllocationID });
                cmd.Parameters.Add(new MySqlConnector.MySqlParameter("p_UserID", MySqlConnector.MySqlDbType.Int32) { Value = (object?)req.UserID ?? DBNull.Value });
                cmd.Parameters.Add(new MySqlConnector.MySqlParameter("p_Reason", MySqlConnector.MySqlDbType.VarChar) { Value = "Ανάκληση δέσμευσης γραμμής παραγγελίας" });
                await cmd.ExecuteNonQueryAsync();
            }

            await _context.Entry(line).ReloadAsync();
            line.LineStatus = RecomputeLineStatus(line);
            line.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var order = await _context.DoctorOrders.FindAsync(line.OrderID);
            if (order != null)
            {
                await RecomputeOrderStatus(order);
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

        // ---- Ενημέρωση τιμολόγησης (τύπος + σχόλιο), χωρίς να πειράξει τίποτα άλλο ----
        [HttpPost("update-invoice")]
        public async Task<ActionResult> UpdateInvoice([FromBody] UpdateInvoiceRequest req)
        {
            var order = await _context.DoctorOrders.FindAsync(req.OrderID);
            if (order == null) return NotFound();
            order.InvoiceType = string.IsNullOrEmpty(req.InvoiceType) ? "Κανονικό" : req.InvoiceType;
            order.InvoiceNote = req.InvoiceNote;
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
                // Η παραγγελία θεωρείται "με σφάλμα" ΜΟΝΟ αν δεν έχει ΚΑΜΙΑ έγκυρη γραμμή
                // ή αν ο τύπος προϊόντος δεν αναγνωρίστηκε - σε αντίθετη περίπτωση
                // περνάει κανονικά με μόνο τις έγκυρες γραμμές
                g.HasErrors = !g.ProductTypeValid || !g.Lines.Any(l => l.CodeValid);

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

                foreach (var l in g.Lines.Where(l => l.CodeValid))
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

            // Τα γράμματα διαβάζονται ΜΙΑ φορά από τη βάση και το σύνολο ενημερώνεται
            // in-memory: η μετονομασία της αρχικής σε -A δεν έχει αποθηκευτεί ακόμα,
            // οπότε ένα δεύτερο query στη βάση δεν θα την έβλεπε και θα ξαναέδινε
            // το ίδιο γράμμα στη νέα παραγγελία (duplicate key στο uq_order_code).
            var usedLetters = await GetUsedSplitLetters(baseCode);

            if (!HasLetterSuffix(order.OrderCode))
            {
                var letterForOriginal = NextFreeLetter(usedLetters);
                usedLetters.Add(letterForOriginal);
                order.OrderCode = $"{baseCode}-{letterForOriginal}";
            }
            var newLetter = NextFreeLetter(usedLetters);
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Safety net για race condition: αν άλλο request πήρε το ίδιο γράμμα
                // στο μεταξύ, καθαρό μήνυμα αντί για 500.
                _logger.LogError(ex, "Duplicate order code κατά τον διαχωρισμό της {OrderCode}", baseCode);
                return Ok(new ShipResult
                {
                    Success = false,
                    Message = $"Ο κωδικός {newOrderCode} μόλις χρησιμοποιήθηκε από άλλη ενέργεια. Δοκίμασε ξανά τον διαχωρισμό."
                });
            }

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

                // Το υπόλοιπο της αρχικής γραμμής "κλειδώνει" στο ό,τι δεσμεύτηκε.
                // Γραμμή που δεν είχε ούτε δέσμευση ούτε ακύρωση θα έμενε με 0 τεμ. --
                // σβήνεται από την αρχική (το σύνολό της μεταφέρθηκε στη νέα παραγγελία).
                pl.QuantityRequested = pl.QuantityAllocated + pl.QuantityCancelled;
                if (pl.QuantityRequested == 0)
                {
                    _context.DoctorOrderLines.Remove(pl);
                }
                else
                {
                    pl.LineStatus = "Fulfilled";
                    pl.UpdatedAt = DateTime.Now;
                }
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

        // Όλα τα split γράμματα που υπάρχουν ήδη στη βάση για το base code
        // (πιάνει και τις δύο περιπτώσεις: αρχική χωρίς suffix ή ήδη -A/-B...).
        private async Task<HashSet<char>> GetUsedSplitLetters(string baseCode)
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
            return used;
        }

        private static char NextFreeLetter(HashSet<char> used)
        {
            var next = 'A';
            while (used.Contains(next)) next++;
            return next;
        }

        // ---- Φύλλο προετοιμασίας παραγγελίας (picking list) σε PDF ----
        // Τυπώνεται για να μαζέψει η αποθήκη τα είδη: ReadyToShip -> ό,τι δεσμεύτηκε,
        // αλλιώς -> ζητούμενο μείον ακυρωμένο. Ακυρωμένες γραμμές δεν τυπώνονται.
        [HttpGet("print/{orderId}")]
        public async Task<IActionResult> PrintPickingSheet(long orderId)
        {
            var order = await _context.DoctorOrders.Include(o => o.Doctor)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);
            if (order == null) return NotFound();

            var lines = await _context.DoctorOrderLines.Where(l => l.OrderID == orderId).ToListAsync();
            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            int QtyToPick(DoctorOrderLine l) => order.OrderStatus == "ReadyToShip"
                ? l.QuantityAllocated
                : l.QuantityRequested - l.QuantityCancelled;

            var printLines = lines.Where(l => QtyToPick(l) > 0).ToList();
            if (printLines.Count == 0)
                return BadRequest("Δεν υπάρχουν είδη για προετοιμασία σε αυτή την παραγγελία.");

            QuestPDF.Settings.License = LicenseType.Community;

            var doctorName = order.Doctor?.FullName ?? order.DoctorName ?? "";
            var accent = order.Company == "SM" ? "#28a745" : "#b57bee";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Background(accent).Padding(10).Row(row =>
                        {
                            row.RelativeItem().Text("ΦΥΛΛΟ ΠΡΟΕΤΟΙΜΑΣΙΑΣ ΠΑΡΑΓΓΕΛΙΑΣ")
                                .FontSize(14).Bold().FontColor("#FFFFFF");
                            row.ConstantItem(120).AlignRight().Text(order.OrderCode)
                                .FontSize(14).Bold().FontColor("#FFFFFF");
                        });
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Γιατρός/Πελάτης: {doctorName}").Bold();
                                if (!string.IsNullOrWhiteSpace(order.RecipientName))
                                    c.Item().Text($"Παραλήπτης: {order.RecipientName}").FontSize(9);
                                var addr = $"{order.ShippingAddress} {order.ShippingCity} {order.ShippingPostalCode}".Trim();
                                if (addr.Length > 0)
                                    c.Item().Text($"Διεύθυνση: {addr}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(order.ShippingPhone))
                                    c.Item().Text($"Τηλέφωνο: {order.ShippingPhone}").FontSize(9);
                            });
                            row.ConstantItem(170).AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text($"Εταιρεία: {order.Company}").FontSize(9);
                                c.Item().AlignRight().Text($"Ημ/νία παραγγελίας: {order.OrderDate:dd/MM/yyyy}").FontSize(9);
                                c.Item().AlignRight().Text($"Κατάσταση: {order.OrderStatus}").FontSize(9);
                                if (!string.IsNullOrWhiteSpace(order.Notes))
                                    c.Item().AlignRight().Text($"Σημ.: {order.Notes}").FontSize(8).Italic();
                            });
                        });
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#000000");
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(28);   // Α/Α
                                columns.ConstantColumn(70);   // Κωδικός
                                columns.RelativeColumn();     // Αλλεργιογόνο
                                columns.RelativeColumn();     // Τύπος
                                columns.ConstantColumn(50);   // Ποσότητα
                                columns.ConstantColumn(40);   // ✓
                            });

                            table.Header(header =>
                            {
                                void HeaderCell(string text)
                                {
                                    header.Cell().BorderBottom(1.5f).BorderColor("#000000")
                                        .PaddingVertical(5).PaddingHorizontal(3)
                                        .Text(text).FontSize(9).Bold();
                                }

                                HeaderCell("Α/Α");
                                HeaderCell("Κωδικός");
                                HeaderCell("Αλλεργιογόνο");
                                HeaderCell("Τύπος");
                                HeaderCell("Τεμ.");
                                HeaderCell("✓");
                            });

                            int idx = 0;
                            foreach (var line in printLines)
                            {
                                allergenLookup.TryGetValue(line.CodePrick, out var allergen);
                                productLookup.TryGetValue(line.ProductTypeCode, out var product);
                                var bg = idx % 2 == 0 ? "#FFFFFF" : "#F2F2F2";

                                void DataCell(string text, bool bold = false)
                                {
                                    var t = table.Cell().Background(bg)
                                        .BorderBottom(1).BorderColor("#E0E0E0")
                                        .PaddingVertical(6).PaddingHorizontal(3)
                                        .Text(text).FontSize(10);
                                    if (bold) t.Bold();
                                }

                                DataCell((idx + 1).ToString());
                                DataCell(line.CodePrick, bold: true);
                                DataCell(allergen?.DescriptionGreek ?? allergen?.Description ?? "");
                                DataCell(product?.Description ?? line.ProductTypeCode);
                                DataCell(QtyToPick(line).ToString(), bold: true);
                                DataCell("☐");

                                idx++;
                            }
                        });

                        col.Item().PaddingTop(10).AlignRight()
                            .Text($"Σύνολο: {printLines.Sum(QtyToPick)} τεμ. σε {printLines.Count} κωδικούς")
                            .FontSize(10).Bold();
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor("#000000");
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text($"Εκτύπωση: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(9);
                            row.ConstantItem(220).AlignRight()
                                .Text("Ετοίμασε: ____________________").FontSize(9);
                        });
                    });
                });
            });

            using var stream = new MemoryStream();
            document.GeneratePdf(stream);
            return File(stream.ToArray(), "application/pdf", $"{order.OrderCode}_Προετοιμασία.pdf");
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