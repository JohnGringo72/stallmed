namespace StallmedManager.Shared.Models
{
    public class PrickStockLevelDto
    {
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public string? Company { get; set; }
        public int TotalRemaining { get; set; }
    }

    public class PrickAgingLineDto
    {
        public string OrderCode { get; set; }
        public string? DoctorName { get; set; }
        public string CodePrick { get; set; }
        public string ProductTypeCode { get; set; }
        public int QuantityRequested { get; set; }
        public int QuantityAllocated { get; set; }
        public int QuantityCancelled { get; set; }
        public int QuantityPending => QuantityRequested - QuantityAllocated - QuantityCancelled;
        public DateTime OrderDate { get; set; }
        public int DaysPending { get; set; }
    }

    public class PrickDashboardSummaryDto
    {
        public int PendingDoctorOrderLinesCount { get; set; }
        public int OpenProductionOrdersCount { get; set; }
        public List<PrickStockLevelDto> StockLevels { get; set; } = new();
        public List<PrickAgingLineDto> AgingLines { get; set; } = new();
    }

    public class LiveStockItemDto
    {
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public string? Company { get; set; }
        public int TotalReceived { get; set; }
        public int TotalAllocated { get; set; }
        public int FreeStock { get; set; } // = QuantityRemaining (μη δεσμευμένο)
    }

    public class SmartStockProposalDto
    {
        public string CodePrick { get; set; }
        public string? AllergenDescription { get; set; }
        public string ProductTypeCode { get; set; }
        public string? ProductDescription { get; set; }
        public string Company { get; set; }
        public int WeeklyAvg { get; set; }        // μέσος εβδομαδιαίος όγκος (90 ημέρες / 13 εβδ.)
        public int SecurityStock { get; set; }     // 12% του μέσου όρου
        public int CurrentStock { get; set; }      // ελεύθερο stock
        public int AlreadyOrdered { get; set; }    // ήδη σε παραγγελία στοκ (Open/PartiallyReceived)
        public int PendingDemand { get; set; }     // εκκρεμές από γιατρούς
        public int Proposed { get; set; }          // τελική πρόταση
        public int OrderQuantity { get; set; }     // editable by user
    }
}
