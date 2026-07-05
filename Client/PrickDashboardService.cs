using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PrickDashboardService
    {
        private readonly DataService dataService;

        public PrickDashboardService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<PrickDashboardSummaryDto> GetSummary(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<PrickDashboardSummaryDto>($"api/prickdashboard/summary{query}");
        }
    }
}
