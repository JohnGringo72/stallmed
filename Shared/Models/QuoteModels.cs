using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    // Κεφαλίδα προσφοράς. Σε καμία κατάσταση εκτός Converted δεν αγγίζει
    // απόθεμα ή λογιστικά -- η δέσμευση γίνεται μόνο μέσω της DoctorOrder
    // που δημιουργείται κατά τη μετατροπή.
    [Table("Quotes")]
    public class Quote
    {
        [Key]
        public long QuoteID { get; set; }
        public string QuoteNumber { get; set; }
        public string Company { get; set; }
        public string Status { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime ValidUntil { get; set; }

        // Ο πελάτης-νοσοκομείο είναι εγγραφή στον πίνακα Doctors (εκεί ζουν
        // ήδη τα ονόματα των νοσοκομείων μαζί με τους γιατρούς).
        public int? CustomerDoctorID { get; set; }
        [ForeignKey("CustomerDoctorID")]
        public Doctor? CustomerDoctor { get; set; }
        // Snapshot στοιχείων πελάτη τη στιγμή της έκδοσης -- η προσφορά μένει
        // σωστή ακόμα κι αν αλλάξουν αργότερα τα στοιχεία του πελάτη.
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

        public decimal Subtotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal Total { get; set; }

        public string? TermsDelivery { get; set; }
        public string? TermsPayment { get; set; }
        public string? TermsWarranty { get; set; }

        public string? PdfPath { get; set; }
        public byte[]? PdfData { get; set; }

        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("QuoteLines")]
    public class QuoteLine
    {
        [Key]
        public long QuoteLineID { get; set; }
        public long QuoteID { get; set; }
        [ForeignKey("QuoteID")]
        public Quote? Quote { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
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

    // Συνημμένα προσφοράς (π.χ. ζήτηση νοσοκομείου) -- ίδιο pattern με τα
    // DoctorOrderAttachments: το αρχείο αποθηκεύεται ως blob στη βάση.
    [Table("QuoteAttachments")]
    public class QuoteAttachment
    {
        [Key]
        public long AttachmentID { get; set; }
        public long QuoteID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[] FileData { get; set; }
        public int? UploadedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Ιστορικό ενεργειών προσφοράς (δημιουργία, αλλαγές κατάστασης, PDF, email).
    [Table("QuoteEvents")]
    public class QuoteEvent
    {
        [Key]
        public long QuoteEventID { get; set; }
        public long QuoteID { get; set; }
        public string EventType { get; set; }
        public string? Details { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
