namespace StallmedManager.Shared.Models
{
    public class DoctorStats
    {
        public string Doctor { get; set; } = "";
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
        public List<StatusCount> PerStatus { get; set; } = new();
        public List<ProductCount> PerProduct { get; set; } = new();
        public List<WebOrder> Orders { get; set; } = new();
    }

    public class MonthlyCount
    {
        public string Month { get; set; } = "";
        public int Count { get; set; }
    }

    public class StatusCount
    {
        public string Status { get; set; } = "";
        public string StatusLabel { get; set; } = "";
        public string Color { get; set; } = "";
        public int Count { get; set; }
    }

    public class ProductCount
    {
        public string Product { get; set; } = "";
        public int Count { get; set; }
    }
}