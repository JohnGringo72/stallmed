namespace StallmedManager.Shared.Models
{
    public class PendingOrderLineDto
    {
        public long OrderLineID { get; set; }
        public string OrderCode { get; set; }
        public string? DoctorName { get; set; }
        public string Company { get; set; }
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public int QuantityRequested { get; set; }
        public int QuantityAllocated { get; set; }
        public int QuantityCancelled { get; set; }
        public int QuantityPending => QuantityRequested - QuantityAllocated - QuantityCancelled;
        public int AvailableStock { get; set; }
        public DateTime OrderDate { get; set; }
    }

    public class AllocateRequest
    {
        public long OrderLineID { get; set; }
        public int Quantity { get; set; }
        public int? UserID { get; set; }
    }

    public class AllocateResult
    {
        public int QuantityActuallyAllocated { get; set; }
    }

    public class ActiveAllocationDto
    {
        public long AllocationID { get; set; }
        public string OrderCode { get; set; }
        public string? DoctorName { get; set; }
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityAllocated { get; set; }
        public DateTime AllocationDate { get; set; }
        public DateTime ReceiptDate { get; set; }
    }

    public class ReverseAllocationRequest
    {
        public long AllocationID { get; set; }
        public int? UserID { get; set; }
        public string? Reason { get; set; }
    }

    public class DoctorOptionDto
    {
        public int DoctorID { get; set; }
        public string FullName { get; set; }
    }
}
