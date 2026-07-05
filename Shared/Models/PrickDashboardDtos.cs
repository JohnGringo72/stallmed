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
}
