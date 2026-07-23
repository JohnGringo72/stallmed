namespace StallmedManager.Shared.Models
{
    public class QuoteLineViewDto
    {
        public long QuoteLineID { get; set; }
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; } = "τεμ.";
        public decimal UnitPrice { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal VatRate { get; set; }
        public decimal LineNet { get; set; }
        public decimal LineVat { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class QuoteViewDto
    {
        public long QuoteID { get; set; }
        public string QuoteNumber { get; set; }
        public string Company { get; set; }
        public string Status { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ValidUntil { get; set; }
        public int? CustomerDoctorID { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerVat { get; set; }
        public string? CustomerDepartment { get; set; }
        public string? CustomerContact { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerPhone { get; set; }
        public string? HospitalRequestRef { get; set; }
        public string? Notes { get; set; }
        public string? RejectReason { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public long? ConvertedOrderID { get; set; }
        public string? ConvertedOrderCode { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal Total { get; set; }
        public string? TermsDelivery { get; set; }
        public string? TermsPayment { get; set; }
        public string? TermsWarranty { get; set; }
        public string? PdfPath { get; set; }
        public bool HasPdf { get; set; }
        public int AttachmentCount { get; set; }
        public List<QuoteLineViewDto> Lines { get; set; } = new();
    }

    public class SaveQuoteLineRequest
    {
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal VatRate { get; set; }
    }

    public class SaveQuoteRequest
    {
        public string Company { get; set; }
        public int? CustomerDoctorID { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime? ValidUntil { get; set; }
        public string? HospitalRequestRef { get; set; }
        public string? Notes { get; set; }
        public string? TermsDelivery { get; set; }
        public string? TermsPayment { get; set; }
        public string? TermsWarranty { get; set; }
        public int? CreatedBy { get; set; }
        public List<SaveQuoteLineRequest> Lines { get; set; } = new();
    }

    public class QuoteActionRequest
    {
        public int? UserID { get; set; }
        public string? Reason { get; set; }
    }

    public class QuoteActionResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public long? OrderID { get; set; }
        public string? OrderCode { get; set; }
        public string? PdfPath { get; set; }
    }

    // Πελάτης-νοσοκομείο = εγγραφή του πίνακα Doctors (τα ονόματα υπάρχουν ήδη εκεί).
    public class CustomerDto
    {
        public int DoctorID { get; set; }
        public string Name { get; set; }
        public string? VatNumber { get; set; }
        public string? Department { get; set; }
        public string? ContactPerson { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? PostalCode { get; set; }
    }

    // ---- Εισαγωγή γραμμών προσφοράς από Excel (μέσα στη φόρμα) ----
    public class QuoteImportLinePreview
    {
        public string CodePrick { get; set; } = "";
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; } = "";
        public string? ProductDescription { get; set; }
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal VatRate { get; set; }
        public bool IsValid { get; set; }
        public string? Warning { get; set; }
    }

    public class QuoteImportPreviewResult
    {
        public List<QuoteImportLinePreview> Lines { get; set; } = new();
        public int TotalRows { get; set; }
        public int ErrorRows { get; set; }
    }

    public class QuoteProductOptionDto
    {
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
    }

    public class QuoteProductTypeOptionDto
    {
        public string ProductTypeCode { get; set; }
        public string? Description { get; set; }
        public decimal? PublicPrice { get; set; }
        public decimal? ExFactoryPrice { get; set; }
    }
}
