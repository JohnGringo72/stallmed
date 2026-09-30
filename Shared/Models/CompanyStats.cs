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
        public int PrevUniquePatients { get; set; }
        public double PatientsTrendPercent { get; set; }
        // Ανάλυση παραγγελιών ανά εταιρεία (CompanyID "1"=SM, "2"=BM)
        public int CurrentOrdersSM { get; set; }
        public int CurrentOrdersBM { get; set; }
        public int PrevOrdersSM { get; set; }
        public int PrevOrdersBM { get; set; }
        public int PrevTotalOrders { get; set; }
        public double OrdersTrendPercent { get; set; }
        // Νέοι ασθενείς της προηγούμενης ισόχρονης περιόδου
        public int PrevNewPatients { get; set; }
        public int NewPolymerizedPatients { get; set; }
        public int NewPolymerizedQNT { get; set; }
        public List<ProductCount> PolymerizedProducts { get; set; } = new();
        public List<MonthlyCount> PerMonth { get; set; } = new();
        public List<MonthlyCount> PerMonthPrev { get; set; } = new();
        public List<ProductCount> PerProduct { get; set; } = new();
    }

    // ---- Σύγκριση ετών: πάντα 12 μήνες στον άξονα, μία σειρά ανά χρονιά ----
    public class YearlyMonthlyStats
    {
        public List<int> AvailableYears { get; set; } = new();
        public List<YearSeries> Series { get; set; } = new();
    }

    public class YearSeries
    {
        public int Year { get; set; }
        // Πάντα 12 τιμές: Ιανουάριος -> Δεκέμβριος
        public List<int> Counts { get; set; } = new();
        public int Total { get; set; }
    }
}
