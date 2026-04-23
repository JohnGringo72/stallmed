namespace StallmedManager.Models;

public class StockSearchResult
{
    public string TreatmentDescription { get; set; } = "";
    public string? Allergen { get; set; }
    public string CompanyID { get; set; } = "";
    public int TotalQNT { get; set; }
}
