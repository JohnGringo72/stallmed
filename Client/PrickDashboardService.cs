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

        public async Task<List<LiveStockItemDto>> GetLiveStock(string? company, string? productTypeCode, string? search)
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(company)) query.Add($"company={Uri.EscapeDataString(company)}");
            if (!string.IsNullOrEmpty(productTypeCode)) query.Add($"productTypeCode={Uri.EscapeDataString(productTypeCode)}");
            if (!string.IsNullOrEmpty(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : "";
            return await dataService.Get<List<LiveStockItemDto>>($"api/prickdashboard/live-stock{queryString}");
        }

        public async Task<List<ProductType>> GetProductTypes()
        {
            return await dataService.Get<List<ProductType>>("api/prickdashboard/product-types");
        }

        public async Task<List<SmartStockProposalDto>> GetSmartStockProposal(string? company, string? productTypeCode)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={Uri.EscapeDataString(company)}");
            if (!string.IsNullOrEmpty(productTypeCode)) qs.Add($"productTypeCode={Uri.EscapeDataString(productTypeCode)}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<SmartStockProposalDto>>($"api/prickdashboard/smart-stock-proposal{query}") ?? new();
        }

        public async Task<byte[]> ExportSmartStockProposalExcel(string company, string productTypeCode, List<SmartStockProposalDto> items)
        {
            return await dataService.PostBytes(
                $"api/prickdashboard/smart-stock-proposal-export?company={Uri.EscapeDataString(company)}&productTypeCode={Uri.EscapeDataString(productTypeCode)}",
                items);
        }
    }
}
