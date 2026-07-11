namespace StallmedManager.Shared.Models
{
    public class DoctorOrderLineViewDto
    {
        public long OrderLineID { get; set; }
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public int QuantityRequested { get; set; }
        public int QuantityAllocated { get; set; }
        public int QuantityCancelled { get; set; }
        public string LineStatus { get; set; }
        public int AvailableStock { get; set; }
        public int ElsewhereQuantity { get; set; }
    }

    public class DoctorOrderViewDto
    {
        public long OrderID { get; set; }
        public string OrderCode { get; set; }
        public string? DoctorName { get; set; }
        public string Company { get; set; }
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; }
        public DateTime? ShippedAt { get; set; }
        public string? CourierTrackingCode { get; set; }
        public string? ShippingCarrier { get; set; }
        public string? DeliveryPersonName { get; set; }
        public string? RecipientName { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingPhone { get; set; }
        public string? Notes { get; set; }
        public string? InvoiceType { get; set; }
        public string? InvoiceNote { get; set; }
        public int AttachmentCount { get; set; }
        public List<DoctorOrderLineViewDto> Lines { get; set; } = new();
    }

    public class CreateDoctorOrderLineRequest
    {
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityRequested { get; set; }
    }

    public class CreateDoctorOrderRequest
    {
        public string Company { get; set; }
        public int? DoctorID { get; set; }
        public DateTime OrderDate { get; set; }
        public string? Notes { get; set; }
        public int? CreatedBy { get; set; }
        public string? RecipientName { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingPostalCode { get; set; }
        public string? ShippingPhone { get; set; }
        public string? InvoiceType { get; set; }
        public string? InvoiceNote { get; set; }
        public List<CreateDoctorOrderLineRequest> Lines { get; set; } = new();
    }

    public class UpdateInvoiceRequest
    {
        public long OrderID { get; set; }
        public string? InvoiceType { get; set; }
        public string? InvoiceNote { get; set; }
    }

    public class ShipOrderRequest
    {
        public long OrderID { get; set; }
        public int? UserID { get; set; }
    }

    public class ShipResult
    {
        public bool Success { get; set; }
        public string? NewOrderCode { get; set; }
    }

    public class QuickAddDoctorRequest
    {
        public string FullName { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public int? CreatedBy { get; set; }
    }

    public class ImportOrderLinePreview
    {
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public int Quantity { get; set; }
        public bool CodeValid { get; set; }
    }

    public class ImportOrderGroupPreview
    {
        public string Company { get; set; }
        public string DoctorNameRaw { get; set; }
        public int? MatchedDoctorId { get; set; }
        public bool IsNewDoctor { get; set; }
        public string ProductTypeCode { get; set; }
        public bool ProductTypeValid { get; set; }
        public DateTime OrderDate { get; set; }
        public List<ImportOrderLinePreview> Lines { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool HasErrors { get; set; }
    }

    public class ImportPreviewResult
    {
        public List<ImportOrderGroupPreview> Groups { get; set; } = new();
        public int TotalRows { get; set; }
        public int ErrorRows { get; set; }
    }

    public class CommitImportRequest
    {
        public List<ImportOrderGroupPreview> Groups { get; set; } = new();
        public int? CreatedBy { get; set; }
    }
}
