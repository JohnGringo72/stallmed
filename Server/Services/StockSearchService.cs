using Microsoft.EntityFrameworkCore;
using StallmedManager.Server.Models;
using StallmedManager.Models;

namespace StallmedManager.Services;

public class StockSearchService
{
    private readonly StallmedContext _context;

    public StockSearchService(StallmedContext context)
    {
        _context = context;
    }

    public async Task<StockFilterOptions> GetFilterOptionsAsync(string companyID, string treatment)
    {
        var base_q = _context.WebOrders.Where(x => x.Patient == "A A");

        if (!string.IsNullOrWhiteSpace(companyID))
            base_q = base_q.Where(x => x.CompanyID == companyID);

        if (!string.IsNullOrWhiteSpace(treatment))
            base_q = base_q.Where(x => x.TreatmentDescription == treatment);

        return new StockFilterOptions
        {
            Treatments = await _context.WebOrders
                .Where(x => x.Patient == "A A")
                .Where(x => string.IsNullOrWhiteSpace(companyID) || x.CompanyID == companyID)
                .Where(x => x.TreatmentDescription != null && x.TreatmentDescription != "")
                .Select(x => x.TreatmentDescription!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(),

            Allergens = await base_q
                .Where(x => x.Allergen != null && x.Allergen != "")
                .Select(x => x.Allergen!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync()
        };
    }

    public async Task<List<StockSearchResult>> SearchAsync(
        string searchText,
        string companyID,
        string treatment,
        string allergen)
    {
        var query = _context.WebOrders
            .Where(x => x.Patient == "A A");

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            query = query.Where(x =>
                (x.TreatmentDescription != null && x.TreatmentDescription.Contains(term)) ||
                (x.Allergen != null && x.Allergen.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(treatment))
            query = query.Where(x => x.TreatmentDescription == treatment);

        if (!string.IsNullOrWhiteSpace(allergen))
            query = query.Where(x => x.Allergen == allergen);

        if (!string.IsNullOrWhiteSpace(companyID))
            query = query.Where(x => x.CompanyID == companyID);

        var results = await query
            .GroupBy(x => new
            {
                x.TreatmentDescription,
                x.Allergen,
                x.CompanyID
            })
            .Select(g => new StockSearchResult
            {
                TreatmentDescription = g.Key.TreatmentDescription ?? "",
                Allergen = g.Key.Allergen,
                CompanyID = g.Key.CompanyID ?? "",
                TotalQNT = g.Sum(x => x.QNT ?? 0)
            })
            .OrderBy(x => x.TreatmentDescription)
            .ThenBy(x => x.CompanyID)
            .ToListAsync();

        return results;
    }
}
