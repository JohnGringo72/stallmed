namespace StallmedManager.Shared.Models
{
    // ---- "Δεσμευμένο αλλού" / Κλέψιμο ----
    public class ElsewhereAllocationDto
    {
        public long AllocationID { get; set; }
        public long OrderLineID { get; set; }
        public string OrderCode { get; set; }
        public string? DoctorName { get; set; }
        public int QuantityAllocated { get; set; }
        public DateTime AllocationDate { get; set; }
    }

    public class StealRequest
    {
        public long SourceAllocationID { get; set; }
        public int Quantity { get; set; }
        public long TargetOrderLineID { get; set; }
        public int? UserID { get; set; }
    }

    public class StealResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    // ---- Ακύρωση γραμμής ----
    public class CancelLineRequest
    {
        public long OrderLineID { get; set; }
        public int? UserID { get; set; }
    }

    // ---- Αναίρεση ακύρωσης γραμμής ----
    public class UncancelLineRequest
    {
        public long OrderLineID { get; set; }
        public int? UserID { get; set; }
    }

    // ---- Απλή ανάκληση όλων των ενεργών δεσμεύσεων μιας γραμμής ----
    public class ReverseLineRequest
    {
        public long OrderLineID { get; set; }
        public int? UserID { get; set; }
    }

    // ---- Επεξεργασία παραγγελίας (notes, προσθήκη/αφαίρεση γραμμών) ----
    public class UpdateNotesRequest
    {
        public long OrderID { get; set; }
        public string? Notes { get; set; }
    }

    public class AddOrderLineRequest
    {
        public long OrderID { get; set; }
        public string CodePrick { get; set; }
        public int Quantity { get; set; }
    }

    public class RemoveOrderLineRequest
    {
        public long OrderLineID { get; set; }
    }

    // ---- Attachments (εικόνες) ----
    public class AttachmentDto
    {
        public long AttachmentID { get; set; }
        public string? FileName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ---- Αποστολές ----
    public class SetShipmentRequest
    {
        public long OrderID { get; set; }
        public string ShippingCarrier { get; set; }
        public string? DeliveryPersonName { get; set; }
        public DateTime ShippedDate { get; set; }
        public int? UserID { get; set; }
    }

    // ---- Διαχειρίσιμη λίστα τρόπων αποστολής (courier) ----
    public class ShippingCourierDto
    {
        public int CourierID { get; set; }
        public string Name { get; set; }
        public bool IsOwnMeans { get; set; }
    }

    public class AddCourierRequest
    {
        public string Name { get; set; }
    }

    // ---- Διαχειρίσιμη λίστα ονομάτων για αποστολή "Ίδια Μέσα" ----
    public class DeliveryPersonDto
    {
        public int PersonID { get; set; }
        public string Name { get; set; }
    }

    public class AddDeliveryPersonRequest
    {
        public string Name { get; set; }
    }
}
