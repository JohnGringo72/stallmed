namespace StallmedManager.Models;

public class StockFilterOptions
{
    public List<string> Treatments { get; set; } = new();
    public List<string> Allergens { get; set; } = new();
}