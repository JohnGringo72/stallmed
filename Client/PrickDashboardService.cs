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

        public async Task<List<StockOrderProposalItemDto>> GetStockOrderProposal(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<StockOrderProposalItemDto>>($"api/prickdashboard/stock-order-proposal{query}");
        }

        public async Task<byte[]> ExportProposalExcel(string company, List<StockOrderProposalItemDto> items)
        {
            return await dataService.Post<List<StockOrderProposalItemDto>, byte[]>(
                $"api/prickdashboard/stock-order-export?company={company}", items);
        }
    }
}
