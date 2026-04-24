namespace StallmedManager.Models;

public class StockSearchResult
{
    public string TreatmentDescription { get; set; } = "";
    public string? Allergen            { get; set; }
    public string CompanyID            { get; set; } = "";
    public int    TotalQNT             { get; set; }
    public List<StockStatusCount> Statuses { get; set; } = new();
}

public class StockStatusCount
{
    public string? Status { get; set; }
    public int     QNT    { get; set; }
}
