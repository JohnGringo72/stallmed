using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StallmedManager.Shared.Models
{
    [Table("AllergenCodes")]
    public class AllergenCode
    {
        [Key]
        public string CodePrick { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public string? DescriptionOther { get; set; }
        public string? DescriptionGreek { get; set; }
        public string? Category { get; set; }
        public string? GroupEN { get; set; }
        public string? GroupGreek { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("ProductTypes")]
    public class ProductType
    {
        [Key]
        public string ProductTypeCode { get; set; }
        public string? Company { get; set; }
        public string? Description { get; set; }
        public string? DescriptionOther { get; set; }
        public decimal? PublicPrice { get; set; }
        public decimal? ExFactoryPrice { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    [Table("Doctors")]
    public class Doctor
    {
        [Key]
        public int DoctorID { get; set; }
        public string FullName { get; set; }
        public int? LegacyIdSM { get; set; }
        public int? LegacyIdBM { get; set; }
        public string? Specialty { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        // Πεδία για πελάτες-νοσοκομεία (Quotes module): οι πελάτες των προσφορών
        // ζουν στον ίδιο πίνακα με τους γιατρούς (μόνο τα ονόματα ήταν περασμένα).
        public string? VatNumber { get; set; }
        public string? Department { get; set; }
        public string? ContactPerson { get; set; }
        public string? PostalCode { get; set; }
        public bool IsActive { get; set; } = true;
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("DoctorOrders")]
    public class DoctorOrder
    {
        [Key]
        public long OrderID { get; set; }
        public string OrderCode { get; set; }
        public int? DoctorID { get; set; }
        [ForeignKey("DoctorID")]
        public Doctor? Doctor { get; set; }
        public string? DoctorName { get; set; }
        public string? RecipientName { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingPhone { get; set; }
        public string Company { get; set; }
        public DateTime OrderDate { get; set; }
        public int? SalesRepUserID { get; set; }
        public string OrderStatus { get; set; }
        public DateTime? ShippedAt { get; set; }
        public string? CourierTrackingCode { get; set; }
        public string? ShippingCarrier { get; set; }
        public int? DeliveryUserID { get; set; }
        public string? DeliveryPersonName { get; set; }
        public string? Notes { get; set; }
        public string? InvoiceType { get; set; } = "Κανονικό";
        public string? InvoiceNote { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("DoctorOrderLines")]
    public class DoctorOrderLine
    {
        [Key]
        public long OrderLineID { get; set; }
        public long OrderID { get; set; }
        [ForeignKey("OrderID")]
        public DoctorOrder? Order { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityRequested { get; set; }
        public int QuantityAllocated { get; set; }
        public int QuantityCancelled { get; set; }
        public string LineStatus { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("ProductionOrders")]
    public class ProductionOrder
    {
        [Key]
        public long ProductionOrderID { get; set; }
        public string? ProductionOrderCode { get; set; }
        public string Company { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string Status { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("ProductionOrderLines")]
    public class ProductionOrderLine
    {
        [Key]
        public long ProductionOrderLineID { get; set; }
        public long ProductionOrderID { get; set; }
        [ForeignKey("ProductionOrderID")]
        public ProductionOrder? ProductionOrder { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityOrdered { get; set; }
        public int QuantityReceived { get; set; }
        public string LineStatus { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("StockReceipts")]
    public class StockReceipt
    {
        [Key]
        public long ReceiptID { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public DateTime ReceivedDate { get; set; }
        public int QuantityReceived { get; set; }
        public int QuantityRemaining { get; set; }
        public long? ProductionOrderLineID { get; set; }
        public bool IsDepleted { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("StockTransactions")]
    public class StockTransaction
    {
        [Key]
        public long TransactionID { get; set; }
        public long ReceiptID { get; set; }
        public string TransactionType { get; set; }
        public int QuantityChange { get; set; }
        public string? ReferenceType { get; set; }
        public long? ReferenceID { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    [Table("OrderAllocations")]
    public class OrderAllocation
    {
        [Key]
        public long AllocationID { get; set; }
        public long OrderLineID { get; set; }
        public long ReceiptID { get; set; }
        public int QuantityAllocated { get; set; }
        public string AllocationStatus { get; set; }
        public DateTime AllocationDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? ReversedAt { get; set; }
        public int? ReversedBy { get; set; }
        public string? ReverseReason { get; set; }
    }

    [Table("ProductionReceiptAllocations")]
    public class ProductionReceiptAllocation
    {
        [Key]
        public long ReceiptAllocationID { get; set; }
        public long ReceiptID { get; set; }
        public long ProductionOrderLineID { get; set; }
        public int QuantityApplied { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("DoctorOrderAttachments")]
    public class DoctorOrderAttachment
    {
        [Key]
        public long AttachmentID { get; set; }
        public long OrderID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[] ImageData { get; set; }
        public int? UploadedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Διαχειρίσιμη λίστα τρόπων αποστολής (courier), εκτός από το ειδικό "OwnMeans"
    // (Ίδια Μέσα) που οδηγεί σε διαφορετική λογική (επιλογή ονόματος από τη
    // λίστα DeliveryPersons αντί για voucher-φόρμα ACS/Intralink).
    [Table("ShippingCouriers")]
    public class ShippingCourier
    {
        [Key]
        public int CourierID { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        // true μόνο για την ειδική γραμμή "Ίδια Μέσα" -- οδηγεί σε επιλογή ονόματος
        // από τη λίστα DeliveryPersons αντί για τη φόρμα voucher ACS/Intralink.
        public bool IsOwnMeans { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // Όριο αναπαραγγελίας ανά κωδικό+τύπο προϊόντος (Dashboard Αποθέματος).
    // Το φυσικό/δεσμευμένο/παραγγελμένο απόθεμα υπολογίζεται live από τα
    // transactional δεδομένα -- εδώ αποθηκεύεται μόνο το όριο.
    [Table("StockReorderPoints")]
    public class StockReorderPoint
    {
        [Key]
        public long ReorderPointID { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        [Column("ReorderPoint")]
        public int Quantity { get; set; }
        public int? UpdatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Διαχειρίσιμη λίστα ονομάτων για αποστολή "Ίδια Μέσα" -- απλά ονόματα,
    // ΔΕΝ συνδέεται με τον πίνακα Users/λογαριασμούς σύνδεσης.
    [Table("DeliveryPersons")]
    public class DeliveryPerson
    {
        [Key]
        public int PersonID { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}
