using System.Net.Http.Json;
using StallmedManager.Models;

namespace StallmedManager.Client.Services;

public class StockSearchClientService
{
    private readonly HttpClient _http;

    public StockSearchClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<StockFilterOptions> GetFilterOptionsAsync(string companyID = "", string treatment = "")
    {
        var url = $"StockSearch/options?companyID={Uri.EscapeDataString(companyID)}&treatment={Uri.EscapeDataString(treatment)}";
        var result = await _http.GetFromJsonAsync<StockFilterOptions>(url);
        return result ?? new StockFilterOptions();
    }

    public async Task<List<StockSearchResult>> SearchAsync(
        string searchText,
        string companyID,
        string treatment,
        string allergen)
    {
        var url = $"StockSearch?searchText={Uri.EscapeDataString(searchText)}" +
                  $"&companyID={Uri.EscapeDataString(companyID)}" +
                  $"&treatment={Uri.EscapeDataString(treatment)}" +
                  $"&allergen={Uri.EscapeDataString(allergen)}";
        var result = await _http.GetFromJsonAsync<List<StockSearchResult>>(url);
        return result ?? new List<StockSearchResult>();
    }
}