using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Server.Services;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Οι προσφορές περιέχουν εμπορικά στοιχεία (τιμές) -- όχι για ρόλο warehouse.
    [Authorize(Policy = "NotWarehouse")]
    public class QuotesController : ControllerBase
    {
        private readonly StallmedContext _context;
        private readonly ILogger<QuotesController> _logger;
        private readonly QuotePdfService _pdfService;
        private readonly QuoteEmailService _emailService;
        private readonly IConfiguration _config;

        public QuotesController(StallmedContext context, ILogger<QuotesController> logger,
            QuotePdfService pdfService, QuoteEmailService emailService, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _pdfService = pdfService;
            _emailService = emailService;
            _config = config;
        }

        // =====================================================================
        // Λίστα / Προβολή
        // =====================================================================

        [HttpGet]
        public async Task<ActionResult<List<QuoteViewDto>>> GetQuotes(
            [FromQuery] string? company, [FromQuery] string? status, [FromQuery] int? customerId,
            [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] string? search)
        {
            try
            {
                await ExpireOverdueQuotes();

                var query = _context.Quotes.AsQueryable();
                if (!string.IsNullOrEmpty(company))
                    query = query.Where(q => q.Company == company);
                if (!string.IsNullOrEmpty(status) && status != "All")
                    query = query.Where(q => q.Status == status);
                if (customerId.HasValue)
                    query = query.Where(q => q.CustomerDoctorID == customerId.Value);
                if (fromDate.HasValue)
                    query = query.Where(q => q.IssueDate >= fromDate.Value.Date);
                if (toDate.HasValue)
                    query = query.Where(q => q.IssueDate <= toDate.Value.Date);
                if (!string.IsNullOrEmpty(search))
                    query = query.Where(q =>
                        q.QuoteNumber.Contains(search) ||
                        (q.CustomerName != null && q.CustomerName.Contains(search)) ||
                        (q.HospitalRequestRef != null && q.HospitalRequestRef.Contains(search)));

                var quotes = await query.OrderByDescending(q => q.IssueDate)
                    .ThenByDescending(q => q.QuoteID).ToListAsync();

                return Ok(await ToViewDtos(quotes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Σφάλμα στο GetQuotes");
                return StatusCode(500, "Σφάλμα φόρτωσης προσφορών. Δοκίμασε ξανά.");
            }
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<QuoteViewDto>> GetQuote(long id)
        {
            await ExpireOverdueQuotes();
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            var dtos = await ToViewDtos(new List<Quote> { quote });
            return Ok(dtos[0]);
        }

        // =====================================================================
        // Δημιουργία / Επεξεργασία (μόνο Draft/Expired)
        // =====================================================================

        [HttpPost]
        public async Task<ActionResult<QuoteViewDto>> CreateQuote([FromBody] SaveQuoteRequest req)
        {
            if (req.Lines == null || req.Lines.Count == 0)
                return BadRequest("Η προσφορά πρέπει να έχει τουλάχιστον μία γραμμή.");
            if (string.IsNullOrEmpty(req.Company))
                return BadRequest("Επίλεξε εταιρεία.");
            if (req.CustomerDoctorID == null)
                return BadRequest("Επίλεξε πελάτη.");

            var customer = await _context.Doctors.FindAsync(req.CustomerDoctorID.Value);
            if (customer == null)
                return BadRequest("Ο πελάτης δεν βρέθηκε.");

            var validityDays = int.TryParse(_config["Quotes:ValidityDays"], out var vd) ? vd : 30;
            var issueDate = (req.IssueDate ?? DateTime.Today).Date;

            var quote = new Quote
            {
                QuoteNumber = await GenerateQuoteNumber(req.Company, issueDate),
                Company = req.Company,
                Status = QuoteStatus.Draft,
                IssueDate = issueDate,
                ValidUntil = (req.ValidUntil ?? issueDate.AddDays(validityDays)).Date,
                CreatedBy = req.CreatedBy,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            ApplyEditableFields(quote, req, customer);

            _context.Quotes.Add(quote);
            await _context.SaveChangesAsync();

            var lines = BuildLines(quote, req.Lines);
            _context.QuoteLines.AddRange(lines);
            QuoteCalculator.ComputeTotals(quote, lines);
            AddEvent(quote.QuoteID, "Created", $"Δημιουργία προσφοράς {quote.QuoteNumber}", req.CreatedBy);
            await _context.SaveChangesAsync();

            var dtos = await ToViewDtos(new List<Quote> { quote });
            return Ok(dtos[0]);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<QuoteViewDto>> UpdateQuote(long id, [FromBody] SaveQuoteRequest req)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (!QuoteStateMachine.CanEdit(quote.Status))
                return BadRequest($"Η προσφορά σε κατάσταση '{QuoteStatus.Label(quote.Status)}' δεν επιτρέπει επεξεργασία.");
            if (req.Lines == null || req.Lines.Count == 0)
                return BadRequest("Η προσφορά πρέπει να έχει τουλάχιστον μία γραμμή.");

            var customer = req.CustomerDoctorID.HasValue
                ? await _context.Doctors.FindAsync(req.CustomerDoctorID.Value) : null;
            if (customer == null)
                return BadRequest("Ο πελάτης δεν βρέθηκε.");

            if (req.IssueDate.HasValue) quote.IssueDate = req.IssueDate.Value.Date;
            if (req.ValidUntil.HasValue) quote.ValidUntil = req.ValidUntil.Value.Date;
            ApplyEditableFields(quote, req, customer);
            quote.UpdatedAt = DateTime.Now;

            var oldLines = await _context.QuoteLines.Where(l => l.QuoteID == id).ToListAsync();
            _context.QuoteLines.RemoveRange(oldLines);
            var lines = BuildLines(quote, req.Lines);
            _context.QuoteLines.AddRange(lines);
            QuoteCalculator.ComputeTotals(quote, lines);
            AddEvent(id, "Updated", "Επεξεργασία προσφοράς", req.CreatedBy);
            await _context.SaveChangesAsync();

            var dtos = await ToViewDtos(new List<Quote> { quote });
            return Ok(dtos[0]);
        }

        // =====================================================================
        // PDF
        // =====================================================================

        // Παράγει το PDF, το αποθηκεύει στη βάση + στον φάκελο αρχειοθέτησης
        // και το επιστρέφει για λήψη.
        [HttpPost("{id:long}/pdf")]
        public async Task<IActionResult> GeneratePdf(long id, [FromBody] QuoteActionRequest req)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();

            try
            {
                var pdfBytes = await GenerateAndArchivePdf(quote, req.UserID);
                await _context.SaveChangesAsync();
                return File(pdfBytes, "application/pdf", $"{quote.QuoteNumber}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Σφάλμα παραγωγής PDF για προσφορά {QuoteID}", id);
                return StatusCode(500, "Σφάλμα παραγωγής PDF.");
            }
        }

        // Λήψη του ήδη αποθηκευμένου PDF.
        [HttpGet("{id:long}/pdf")]
        public async Task<IActionResult> DownloadPdf(long id)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (quote.PdfData == null || quote.PdfData.Length == 0)
                return NotFound("Δεν έχει εκδοθεί PDF για την προσφορά.");
            return File(quote.PdfData, "application/pdf", $"{quote.QuoteNumber}.pdf");
        }

        // =====================================================================
        // Αποστολή email (Draft -> Sent)
        // =====================================================================

        [HttpPost("{id:long}/send")]
        public async Task<ActionResult<QuoteActionResult>> SendQuote(long id, [FromBody] QuoteActionRequest req)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (!QuoteStateMachine.CanTransition(quote.Status, QuoteStatus.Sent))
                return BadRequest($"Δεν επιτρέπεται αποστολή από κατάσταση '{QuoteStatus.Label(quote.Status)}'.");
            if (string.IsNullOrEmpty(quote.CustomerEmail))
                return BadRequest("Ο πελάτης δεν έχει email. Συμπλήρωσέ το πριν την αποστολή.");
            if (!_emailService.IsConfigured(quote.Company))
                return BadRequest($"Δεν έχουν ρυθμιστεί τα στοιχεία SMTP για την εταιρεία {quote.Company} (appsettings: Smtp / Smtp:{quote.Company}).");

            try
            {
                var pdfBytes = await GenerateAndArchivePdf(quote, req.UserID);

                var companyProfile = _pdfService.GetCompanyProfile(quote.Company);
                var subject = $"Προσφορά {quote.QuoteNumber} – {companyProfile.Name}";
                var body =
                    $"Αξιότιμοι κύριοι/κυρίες,\r\n\r\n" +
                    $"Σας αποστέλλουμε συνημμένα την προσφορά {quote.QuoteNumber}" +
                    (string.IsNullOrEmpty(quote.HospitalRequestRef) ? "" : $" (σχετ.: {quote.HospitalRequestRef})") +
                    $".\r\nΗ προσφορά ισχύει έως {quote.ValidUntil:dd/MM/yyyy}.\r\n\r\n" +
                    $"Παραμένουμε στη διάθεσή σας για κάθε διευκρίνιση.\r\n\r\n" +
                    $"Με εκτίμηση,\r\n{companyProfile.Name}";

                await _emailService.SendAsync(quote.Company, quote.CustomerEmail, quote.CustomerName, subject, body,
                    pdfBytes, $"{quote.QuoteNumber}.pdf");

                // Αντίγραφο του email σε .txt δίπλα στο PDF (best effort).
                SaveEmailCopy(quote, subject, body);

                quote.Status = QuoteStatus.Sent;
                quote.SentAt = DateTime.Now;
                quote.UpdatedAt = DateTime.Now;
                AddEvent(id, "EmailSent", $"Αποστολή στο {quote.CustomerEmail}", req.UserID);
                AddEvent(id, "StatusChanged", $"{QuoteStatus.Draft} -> {QuoteStatus.Sent}", req.UserID);
                await _context.SaveChangesAsync();

                return Ok(new QuoteActionResult { Success = true, Message = $"Η προσφορά εστάλη στο {quote.CustomerEmail}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Σφάλμα αποστολής προσφοράς {QuoteID}", id);
                return StatusCode(500, "Σφάλμα αποστολής email. Έλεγξε τις ρυθμίσεις SMTP.");
            }
        }

        // =====================================================================
        // Μεταβάσεις κατάστασης
        // =====================================================================

        [HttpPost("{id:long}/accept")]
        public async Task<ActionResult<QuoteActionResult>> Accept(long id, [FromBody] QuoteActionRequest req)
            => await Transition(id, QuoteStatus.Accepted, req, setResponded: true);

        [HttpPost("{id:long}/reject")]
        public async Task<ActionResult<QuoteActionResult>> Reject(long id, [FromBody] QuoteActionRequest req)
            => await Transition(id, QuoteStatus.Rejected, req, setResponded: true);

        [HttpPost("{id:long}/expire")]
        public async Task<ActionResult<QuoteActionResult>> Expire(long id, [FromBody] QuoteActionRequest req)
            => await Transition(id, QuoteStatus.Expired, req);

        // Επανέκδοση ληγμένης προσφοράς: Expired -> Draft με νέα ισχύ.
        [HttpPost("{id:long}/reissue")]
        public async Task<ActionResult<QuoteActionResult>> Reissue(long id, [FromBody] QuoteActionRequest req)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (!QuoteStateMachine.CanTransition(quote.Status, QuoteStatus.Draft))
                return BadRequest($"Δεν επιτρέπεται επανέκδοση από κατάσταση '{QuoteStatus.Label(quote.Status)}'.");

            var validityDays = int.TryParse(_config["Quotes:ValidityDays"], out var vd) ? vd : 30;
            var from = quote.Status;
            quote.Status = QuoteStatus.Draft;
            quote.IssueDate = DateTime.Today;
            quote.ValidUntil = DateTime.Today.AddDays(validityDays);
            quote.SentAt = null;
            quote.RespondedAt = null;
            quote.RejectReason = null;
            quote.UpdatedAt = DateTime.Now;
            AddEvent(id, "StatusChanged", $"{from} -> {QuoteStatus.Draft} (επανέκδοση, ισχύς έως {quote.ValidUntil:dd/MM/yyyy})", req.UserID);
            await _context.SaveChangesAsync();

            return Ok(new QuoteActionResult { Success = true });
        }

        private async Task<ActionResult<QuoteActionResult>> Transition(long id, string target, QuoteActionRequest req, bool setResponded = false)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (!QuoteStateMachine.CanTransition(quote.Status, target))
                return BadRequest($"Δεν επιτρέπεται η μετάβαση από '{QuoteStatus.Label(quote.Status)}' σε '{QuoteStatus.Label(target)}'.");

            var from = quote.Status;
            quote.Status = target;
            if (setResponded) quote.RespondedAt = DateTime.Now;
            if (target == QuoteStatus.Rejected && !string.IsNullOrEmpty(req.Reason))
                quote.RejectReason = req.Reason;
            quote.UpdatedAt = DateTime.Now;
            AddEvent(id, "StatusChanged",
                $"{from} -> {target}" + (string.IsNullOrEmpty(req.Reason) ? "" : $" ({req.Reason})"), req.UserID);
            await _context.SaveChangesAsync();

            return Ok(new QuoteActionResult { Success = true });
        }

        // =====================================================================
        // Μετατροπή σε παραγγελία (Accepted -> Converted)
        // =====================================================================

        [HttpPost("{id:long}/convert")]
        public async Task<ActionResult<QuoteActionResult>> Convert(long id, [FromBody] QuoteActionRequest req)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (!QuoteStateMachine.CanTransition(quote.Status, QuoteStatus.Converted))
                return BadRequest($"Δεν επιτρέπεται μετατροπή από κατάσταση '{QuoteStatus.Label(quote.Status)}'.");

            var quoteLines = await _context.QuoteLines.Where(l => l.QuoteID == id).ToListAsync();
            if (quoteLines.Count == 0)
                return BadRequest("Η προσφορά δεν έχει γραμμές.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new DoctorOrder
                {
                    OrderCode = await GenerateOrderCode(quote.Company, DateTime.Today),
                    Company = quote.Company,
                    OrderDate = DateTime.Today,
                    OrderStatus = "Open",
                    DoctorID = quote.CustomerDoctorID,
                    DoctorName = quote.CustomerName,
                    RecipientName = quote.CustomerContact ?? quote.CustomerName,
                    ShippingPhone = quote.CustomerPhone,
                    Notes = $"Από προσφορά {quote.QuoteNumber}",
                    InvoiceType = "Κανονικό",
                    CreatedBy = req.UserID,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.DoctorOrders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var line in quoteLines)
                {
                    _context.DoctorOrderLines.Add(new DoctorOrderLine
                    {
                        OrderID = order.OrderID,
                        CodePrick = line.CodePrick,
                        ProductTypeCode = line.ProductTypeCode,
                        QuantityRequested = line.Quantity,
                        QuantityAllocated = 0,
                        QuantityCancelled = 0,
                        LineStatus = "Pending",
                        Notes = $"Προσφορά {quote.QuoteNumber}",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }

                quote.Status = QuoteStatus.Converted;
                quote.ConvertedOrderID = order.OrderID;
                quote.UpdatedAt = DateTime.Now;
                AddEvent(id, "Converted", $"Μετατροπή σε παραγγελία {order.OrderCode}", req.UserID);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new QuoteActionResult
                {
                    Success = true,
                    OrderID = order.OrderID,
                    OrderCode = order.OrderCode,
                    Message = $"Δημιουργήθηκε η παραγγελία {order.OrderCode}."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Σφάλμα μετατροπής προσφοράς {QuoteID} σε παραγγελία", id);
                return StatusCode(500, "Σφάλμα μετατροπής σε παραγγελία. Δεν έγινε καμία αλλαγή.");
            }
        }

        // =====================================================================
        // Διαγραφή προσφοράς -- μαζί με γραμμές/ιστορικό/συνημμένα. Μια Converted
        // προσφορά δεν διαγράφεται: έχει πίσω της πραγματική παραγγελία.
        // =====================================================================

        [HttpPost("{id:long}/delete")]
        public async Task<ActionResult<QuoteActionResult>> Delete(long id)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null) return NotFound();
            if (quote.Status == QuoteStatus.Converted)
                return BadRequest("Η προσφορά έχει μετατραπεί σε παραγγελία και δεν μπορεί να διαγραφεί.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.QuoteLines.RemoveRange(_context.QuoteLines.Where(l => l.QuoteID == id));
                _context.QuoteEvents.RemoveRange(_context.QuoteEvents.Where(e => e.QuoteID == id));
                _context.QuoteAttachments.RemoveRange(_context.QuoteAttachments.Where(a => a.QuoteID == id));
                _context.Quotes.Remove(quote);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new QuoteActionResult { Success = true, Message = $"Η προσφορά {quote.QuoteNumber} διαγράφηκε." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Σφάλμα διαγραφής προσφοράς {QuoteID}", id);
                return StatusCode(500, "Σφάλμα διαγραφής. Δεν έγινε καμία αλλαγή.");
            }
        }

        // =====================================================================
        // Πελάτες (νοσοκομεία) -- εγγραφές του πίνακα Doctors: τα ονόματα είναι
        // ήδη περασμένα εκεί, τα υπόλοιπα στοιχεία (ΑΦΜ κ.λπ.) συμπληρώνονται εδώ.
        // =====================================================================

        [HttpGet("customers")]
        public async Task<ActionResult<List<CustomerDto>>> GetCustomers([FromQuery] string? search)
        {
            var query = _context.Doctors.Where(d => d.IsActive);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(d => d.FullName.Contains(search) ||
                    (d.VatNumber != null && d.VatNumber.Contains(search)));
            var doctors = await query.OrderBy(d => d.FullName).ToListAsync();
            return Ok(doctors.Select(ToCustomerDto).ToList());
        }

        [HttpPost("customers")]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CustomerDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Η επωνυμία είναι υποχρεωτική.");
            var doctor = new Doctor
            {
                FullName = dto.Name.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            ApplyCustomerFields(doctor, dto);
            _context.Doctors.Add(doctor);
            await _context.SaveChangesAsync();
            return Ok(ToCustomerDto(doctor));
        }

        // Ενημέρωση στοιχείων πελάτη (ΑΦΜ, τμήμα, υπεύθυνος, email...) πάνω
        // στην υπάρχουσα εγγραφή του πίνακα Doctors.
        [HttpPut("customers/{doctorId:int}")]
        public async Task<ActionResult<CustomerDto>> UpdateCustomer(int doctorId, [FromBody] CustomerDto dto)
        {
            var doctor = await _context.Doctors.FindAsync(doctorId);
            if (doctor == null) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Η επωνυμία είναι υποχρεωτική.");
            doctor.FullName = dto.Name.Trim();
            ApplyCustomerFields(doctor, dto);
            await _context.SaveChangesAsync();
            return Ok(ToCustomerDto(doctor));
        }

        // =====================================================================
        // Επιλογές ειδών για τη φόρμα
        // =====================================================================

        [HttpGet("allergens")]
        public async Task<ActionResult<List<QuoteProductOptionDto>>> GetAllergens([FromQuery] string? company)
        {
            var query = _context.AllergenCodes.Where(a => a.IsActive);
            if (!string.IsNullOrEmpty(company))
                query = query.Where(a => a.Company == company);
            var allergens = await query.OrderBy(a => a.CodePrick).ToListAsync();
            return Ok(allergens.Select(a => new QuoteProductOptionDto
            {
                CodePrick = a.CodePrick,
                AllergenDescription = a.DescriptionGreek ?? a.Description
            }).ToList());
        }

        [HttpGet("producttypes")]
        public async Task<ActionResult<List<QuoteProductTypeOptionDto>>> GetProductTypes([FromQuery] string? company)
        {
            var query = _context.ProductTypes.Where(p => p.IsActive);
            if (!string.IsNullOrEmpty(company))
                query = query.Where(p => p.Company == company);
            var types = await query.OrderBy(p => p.ProductTypeCode).ToListAsync();
            return Ok(types.Select(p => new QuoteProductTypeOptionDto
            {
                ProductTypeCode = p.ProductTypeCode,
                Description = p.Description,
                PublicPrice = p.PublicPrice,
                ExFactoryPrice = p.ExFactoryPrice
            }).ToList());
        }

        // =====================================================================
        // Εισαγωγή γραμμών από Excel (γεμίζει τη φόρμα -- δεν γράφει στη βάση)
        // =====================================================================

        [HttpGet("import/template")]
        public ActionResult DownloadImportTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Είδη Προσφοράς");
            ws.Cell(1, 1).Value = "Κωδικός Αλλεργιογόνου";
            ws.Cell(1, 2).Value = "Τύπος (κωδικός)";
            ws.Cell(1, 3).Value = "Περιγραφή (προαιρετικά)";
            ws.Cell(1, 4).Value = "Ποσότητα";
            ws.Cell(1, 5).Value = "Τιμή μονάδας (κενό = από τύπο)";
            ws.Cell(1, 6).Value = "Έκπτωση % (προαιρετικά)";
            ws.Cell(1, 7).Value = "ΦΠΑ % (κενό = 6)";
            ws.Range(1, 1, 1, 7).Style.Font.SetBold();

            // Παράδειγμα γραμμών
            ws.Cell(2, 1).Value = "A-001";
            ws.Cell(2, 2).Value = "91";
            ws.Cell(2, 4).Value = 10;
            ws.Cell(2, 5).Value = 12.50;
            ws.Cell(3, 1).Value = "F-042";
            ws.Cell(3, 2).Value = "91";
            ws.Cell(3, 4).Value = 5;
            ws.Cell(3, 6).Value = 10;

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Πρότυπο_Εισαγωγής_Προσφοράς.xlsx");
        }

        [HttpPost("import/preview")]
        public async Task<ActionResult<QuoteImportPreviewResult>> ImportPreview([FromQuery] string? company, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Δεν στάλθηκε αρχείο.");

            var defaultVat = decimal.TryParse(_config["Quotes:DefaultVatRate"], out var dv) ? dv : 6m;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            var ws = workbook.Worksheets.First();
            var rows = ws.RangeUsed().RowsUsed().Skip(1); // παράλειψη επικεφαλίδων

            var allergens = await _context.AllergenCodes.ToListAsync();
            var productTypes = await _context.ProductTypes.ToListAsync();

            var result = new QuoteImportPreviewResult();

            foreach (var row in rows)
            {
                if (row.IsEmpty()) continue;
                result.TotalRows++;

                var code = row.Cell(1).GetString().Trim().ToUpper();
                var typeCode = row.Cell(2).GetString().Trim();
                var description = row.Cell(3).GetString().Trim();
                int.TryParse(row.Cell(4).GetString().Trim(), out int quantity);
                decimal.TryParse(row.Cell(5).GetString().Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal unitPrice);
                decimal.TryParse(row.Cell(6).GetString().Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal discount);
                var vatRaw = row.Cell(7).GetString().Trim().Replace(',', '.');
                var vatRate = decimal.TryParse(vatRaw,
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal vat) ? vat : defaultVat;

                // Αναζήτηση κωδικού και με την ελληνική περιγραφή, όπως στο import των DoctorOrders
                var matchedAllergen = allergens.FirstOrDefault(a => a.CodePrick.Equals(code, StringComparison.OrdinalIgnoreCase))
                    ?? allergens.FirstOrDefault(a => (a.DescriptionGreek ?? "").Equals(code, StringComparison.OrdinalIgnoreCase));
                var matchedType = productTypes.FirstOrDefault(p => p.ProductTypeCode == typeCode);

                // Χωρίς τιμή στο Excel -> προσυμπλήρωση από τον τύπο προϊόντος
                if (unitPrice == 0 && matchedType != null)
                    unitPrice = matchedType.ExFactoryPrice ?? matchedType.PublicPrice ?? 0;

                string? warning = null;
                if (matchedAllergen == null) warning = "Άγνωστος κωδικός αλλεργιογόνου";
                else if (matchedType == null) warning = "Άγνωστος τύπος προϊόντος";
                else if (quantity <= 0) warning = "Μη έγκυρη ποσότητα";

                var line = new QuoteImportLinePreview
                {
                    CodePrick = matchedAllergen?.CodePrick ?? code,
                    AllergenDescription = matchedAllergen?.DescriptionGreek ?? matchedAllergen?.Description,
                    ProductTypeCode = typeCode,
                    ProductDescription = matchedType?.Description,
                    Description = string.IsNullOrEmpty(description) ? null : description,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    DiscountPct = discount,
                    VatRate = vatRate,
                    IsValid = warning == null,
                    Warning = warning
                };
                if (!line.IsValid) result.ErrorRows++;
                result.Lines.Add(line);
            }

            return Ok(result);
        }

        // =====================================================================
        // Συνημμένα προσφοράς (ίδιο pattern με DoctorOrderAttachments)
        // =====================================================================

        [HttpGet("attachments/{quoteId:long}")]
        public async Task<ActionResult<List<AttachmentDto>>> GetAttachments(long quoteId)
        {
            var list = await _context.QuoteAttachments
                .Where(a => a.QuoteID == quoteId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AttachmentDto { AttachmentID = a.AttachmentID, FileName = a.FileName, CreatedAt = a.CreatedAt })
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost("attachments/{quoteId:long}")]
        public async Task<ActionResult> UploadAttachment(long quoteId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Δεν στάλθηκε αρχείο.");
            var quote = await _context.Quotes.FindAsync(quoteId);
            if (quote == null) return NotFound();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            _context.QuoteAttachments.Add(new QuoteAttachment
            {
                QuoteID = quoteId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileData = ms.ToArray(),
                CreatedAt = DateTime.Now
            });
            AddEvent(quoteId, "AttachmentAdded", file.FileName, null);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("attachments/file/{attachmentId:long}")]
        public async Task<ActionResult> DownloadAttachment(long attachmentId)
        {
            var att = await _context.QuoteAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            return File(att.FileData, att.ContentType ?? "application/octet-stream", att.FileName ?? "attachment");
        }

        [HttpPost("attachments/delete/{attachmentId:long}")]
        public async Task<ActionResult> DeleteAttachment(long attachmentId)
        {
            var att = await _context.QuoteAttachments.FindAsync(attachmentId);
            if (att == null) return NotFound();
            _context.QuoteAttachments.Remove(att);
            AddEvent(att.QuoteID, "AttachmentDeleted", att.FileName, null);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private void ApplyEditableFields(Quote quote, SaveQuoteRequest req, Doctor customer)
        {
            quote.CustomerDoctorID = customer.DoctorID;
            quote.CustomerName = customer.FullName;
            quote.CustomerVat = customer.VatNumber;
            quote.CustomerDepartment = customer.Department;
            quote.CustomerContact = customer.ContactPerson;
            quote.CustomerEmail = customer.Email;
            quote.CustomerPhone = customer.Phone;
            quote.HospitalRequestRef = req.HospitalRequestRef;
            quote.Notes = req.Notes;
            quote.TermsDelivery = req.TermsDelivery;
            quote.TermsPayment = req.TermsPayment;
            quote.TermsWarranty = req.TermsWarranty;
        }

        private static List<QuoteLine> BuildLines(Quote quote, List<SaveQuoteLineRequest> lines)
        {
            return lines.Select(l => new QuoteLine
            {
                Quote = quote,
                CodePrick = l.CodePrick,
                ProductTypeCode = l.ProductTypeCode,
                Description = l.Description,
                Quantity = l.Quantity,
                Unit = string.IsNullOrEmpty(l.Unit) ? "τεμ." : l.Unit,
                UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct,
                VatRate = l.VatRate
            }).ToList();
        }

        private static void ApplyCustomerFields(Doctor doctor, CustomerDto dto)
        {
            doctor.VatNumber = dto.VatNumber;
            doctor.Department = dto.Department;
            doctor.ContactPerson = dto.ContactPerson;
            doctor.Email = dto.Email;
            doctor.Phone = dto.Phone;
            doctor.Address = dto.Address;
            doctor.City = dto.City;
            doctor.PostalCode = dto.PostalCode;
            doctor.UpdatedAt = DateTime.Now;
        }

        private static CustomerDto ToCustomerDto(Doctor d) => new()
        {
            DoctorID = d.DoctorID,
            Name = d.FullName,
            VatNumber = d.VatNumber,
            Department = d.Department,
            ContactPerson = d.ContactPerson,
            Email = d.Email,
            Phone = d.Phone,
            Address = d.Address,
            City = d.City,
            PostalCode = d.PostalCode
        };

        private void AddEvent(long quoteId, string eventType, string? details, int? userId)
        {
            _context.QuoteEvents.Add(new QuoteEvent
            {
                QuoteID = quoteId,
                EventType = eventType,
                Details = details,
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            });
        }

        // Απεσταλμένες προσφορές που πέρασε η ισχύς τους -> Expired (lazy, στο read).
        private async Task ExpireOverdueQuotes()
        {
            var overdue = await _context.Quotes
                .Where(q => q.Status == QuoteStatus.Sent && q.ValidUntil < DateTime.Today)
                .ToListAsync();
            if (overdue.Count == 0) return;
            foreach (var quote in overdue)
            {
                quote.Status = QuoteStatus.Expired;
                quote.UpdatedAt = DateTime.Now;
                AddEvent(quote.QuoteID, "StatusChanged", $"{QuoteStatus.Sent} -> {QuoteStatus.Expired} (αυτόματα, ισχύς έως {quote.ValidUntil:dd/MM/yyyy})", null);
            }
            await _context.SaveChangesAsync();
        }

        private async Task<List<QuoteViewDto>> ToViewDtos(List<Quote> quotes)
        {
            var quoteIds = quotes.Select(q => q.QuoteID).ToList();
            var lines = await _context.QuoteLines
                .Where(l => quoteIds.Contains(l.QuoteID)).ToListAsync();
            var allergenLookup = await _context.AllergenCodes.ToDictionaryAsync(a => a.CodePrick);
            var productLookup = await _context.ProductTypes.ToDictionaryAsync(p => p.ProductTypeCode);

            var orderIds = quotes.Where(q => q.ConvertedOrderID.HasValue)
                .Select(q => q.ConvertedOrderID.Value).ToList();
            var orderCodes = await _context.DoctorOrders
                .Where(o => orderIds.Contains(o.OrderID))
                .ToDictionaryAsync(o => o.OrderID, o => o.OrderCode);

            var attachmentCounts = await _context.QuoteAttachments
                .Where(a => quoteIds.Contains(a.QuoteID))
                .GroupBy(a => a.QuoteID)
                .Select(g => new { QuoteID = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.QuoteID, x => x.Count);

            return quotes.Select(q => new QuoteViewDto
            {
                QuoteID = q.QuoteID,
                QuoteNumber = q.QuoteNumber,
                Company = q.Company,
                Status = q.Status,
                IssueDate = q.IssueDate,
                ValidUntil = q.ValidUntil,
                CustomerDoctorID = q.CustomerDoctorID,
                CustomerName = q.CustomerName,
                CustomerVat = q.CustomerVat,
                CustomerDepartment = q.CustomerDepartment,
                CustomerContact = q.CustomerContact,
                CustomerEmail = q.CustomerEmail,
                CustomerPhone = q.CustomerPhone,
                HospitalRequestRef = q.HospitalRequestRef,
                Notes = q.Notes,
                RejectReason = q.RejectReason,
                SentAt = q.SentAt,
                RespondedAt = q.RespondedAt,
                ConvertedOrderID = q.ConvertedOrderID,
                ConvertedOrderCode = q.ConvertedOrderID.HasValue &&
                    orderCodes.TryGetValue(q.ConvertedOrderID.Value, out var code) ? code : null,
                Subtotal = q.Subtotal,
                VatTotal = q.VatTotal,
                Total = q.Total,
                TermsDelivery = q.TermsDelivery,
                TermsPayment = q.TermsPayment,
                TermsWarranty = q.TermsWarranty,
                PdfPath = q.PdfPath,
                HasPdf = q.PdfData != null && q.PdfData.Length > 0,
                AttachmentCount = attachmentCounts.TryGetValue(q.QuoteID, out var cnt) ? cnt : 0,
                Lines = lines.Where(l => l.QuoteID == q.QuoteID).Select(l =>
                {
                    allergenLookup.TryGetValue(l.CodePrick, out var allergen);
                    productLookup.TryGetValue(l.ProductTypeCode, out var product);
                    return new QuoteLineViewDto
                    {
                        QuoteLineID = l.QuoteLineID,
                        CodePrick = l.CodePrick,
                        AllergenDescription = allergen?.DescriptionGreek ?? allergen?.Description,
                        ProductTypeCode = l.ProductTypeCode,
                        ProductDescription = product?.Description,
                        Description = l.Description,
                        Quantity = l.Quantity,
                        Unit = l.Unit,
                        UnitPrice = l.UnitPrice,
                        DiscountPct = l.DiscountPct,
                        VatRate = l.VatRate,
                        LineNet = l.LineNet,
                        LineVat = l.LineVat,
                        LineTotal = l.LineTotal
                    };
                }).ToList()
            }).ToList();
        }

        // Παράγει το PDF, το κρατά στη βάση (PdfData) και γράφει αντίγραφο στον
        // φάκελο αρχειοθέτησης: {BASE}/Προσφορές/{YYYY}/{Πελάτης}/{QuoteNumber}.pdf
        private async Task<byte[]> GenerateAndArchivePdf(Quote quote, int? userId)
        {
            var dtos = await ToViewDtos(new List<Quote> { quote });
            var pdfBytes = _pdfService.Generate(quote, dtos[0].Lines);

            quote.PdfData = pdfBytes;
            quote.UpdatedAt = DateTime.Now;
            AddEvent(quote.QuoteID, "PdfGenerated", $"Έκδοση PDF {quote.QuoteNumber}.pdf", userId);

            try
            {
                var folder = GetArchiveFolder(quote);
                if (folder != null)
                {
                    Directory.CreateDirectory(folder);
                    var path = Path.Combine(folder, $"{SanitizeFileName(quote.QuoteNumber)}.pdf");
                    await System.IO.File.WriteAllBytesAsync(path, pdfBytes);
                    quote.PdfPath = path;
                }
            }
            catch (Exception ex)
            {
                // Το PDF μένει στη βάση ακόμα κι αν αποτύχει η αρχειοθέτηση στον δίσκο.
                _logger.LogError(ex, "Αποτυχία αρχειοθέτησης PDF προσφοράς {QuoteNumber} στον φάκελο", quote.QuoteNumber);
            }

            return pdfBytes;
        }

        private void SaveEmailCopy(Quote quote, string subject, string body)
        {
            try
            {
                var folder = GetArchiveFolder(quote);
                if (folder == null) return;
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, $"{SanitizeFileName(quote.QuoteNumber)}_email.txt");
                var content = $"Προς: {quote.CustomerEmail}\r\nΘέμα: {subject}\r\nΗμερομηνία: {DateTime.Now:dd/MM/yyyy HH:mm}\r\n\r\n{body}";
                System.IO.File.WriteAllText(path, content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Αποτυχία αποθήκευσης αντιγράφου email για {QuoteNumber}", quote.QuoteNumber);
            }
        }

        private string? GetArchiveFolder(Quote quote)
        {
            var basePath = _config["Quotes:ArchiveBasePath"];
            if (string.IsNullOrEmpty(basePath)) return null;
            var customerFolder = SanitizeFileName(quote.CustomerName ?? "Άγνωστος");
            return Path.Combine(basePath, "Προσφορές", quote.IssueDate.Year.ToString(), customerFolder);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        // ---- Αρίθμηση προσφοράς: ΠΡ-{SM|BM}-{YYYY}-{NNNN}, μηδενισμός ανά εταιρεία+έτος ----
        // π.χ. ΠΡ-SM-2026-0001, ΠΡ-BM-2026-0001 (ίδια λογική retry με το GenerateOrderCode).
        private async Task<string> GenerateQuoteNumber(string company, DateTime issueDate)
        {
            var prefix = $"ΠΡ-{company}-{issueDate.Year}-";

            for (int attempt = 0; attempt < 5; attempt++)
            {
                var existingCount = await _context.Quotes
                    .CountAsync(q => q.QuoteNumber.StartsWith(prefix));

                var sequence = existingCount + 1 + attempt;
                var candidate = $"{prefix}{sequence.ToString().PadLeft(4, '0')}";

                var exists = await _context.Quotes.AnyAsync(q => q.QuoteNumber == candidate);
                if (!exists)
                    return candidate;
            }

            return $"{prefix}{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }

        // ---- Ίδια λογική κωδικού με το PrickDoctorOrderController.GenerateOrderCode ----
        // ώστε οι παραγγελίες από μετατροπή να ακολουθούν την υπάρχουσα αρίθμηση.
        private async Task<string> GenerateOrderCode(string company, DateTime orderDate)
        {
            var datePart = orderDate.ToString("yyMMdd");
            var prefix = $"{company}{datePart}-";

            for (int attempt = 0; attempt < 5; attempt++)
            {
                var existingCount = await _context.DoctorOrders
                    .CountAsync(o => o.OrderCode.StartsWith(prefix));

                var sequence = existingCount + 1 + attempt;
                var digits = sequence > 99 ? 3 : 2;
                var candidate = $"{prefix}{sequence.ToString().PadLeft(digits, '0')}";

                var exists = await _context.DoctorOrders.AnyAsync(o => o.OrderCode == candidate);
                if (!exists)
                    return candidate;
            }

            return $"{prefix}{Guid.NewGuid().ToString("N").Substring(0, 6)}";
        }
    }
}
