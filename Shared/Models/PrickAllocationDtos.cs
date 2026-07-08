namespace StallmedManager.Shared.Models
{
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
