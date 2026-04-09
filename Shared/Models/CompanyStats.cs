namespace StallmedManager.Shared.Models
{
    public class CompanyStats
    {
        public string Company { get; set; } = "";
        public int TotalOrders { get; set; }
        public int TotalQNT { get; set; }
        public int UniquePatients { get; set; }
        public int NewPatients { get; set; }
        public int TotalAllOrders { get; set; }
        public double SharePercent { get; set; }
        public double TrendPercent { get; set; }
        public int PrevTotalQNT { get; set; }
        public double QNTTrendPercent { get; set; }
        public List<MonthlyCount> PerMonth { get; set; } = new();
        public List<MonthlyCount> PerMonthPrev { get; set; } = new();
        public List<ProductCount> PerProduct { get; set; } = new();
    }
}