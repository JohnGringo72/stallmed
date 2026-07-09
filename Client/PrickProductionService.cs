using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PrickProductionService
    {
        private readonly DataService dataService;

        public PrickProductionService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<ProductionOrderDto>> GetOrders(string? company, string? status)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={company}");
            if (!string.IsNullOrEmpty(status)) qs.Add($"status={status}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<ProductionOrderDto>>($"api/prickproduction/orders{query}");
        }

        public async Task<List<SimpleCodeOptionDto>> GetAllergens(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<SimpleCodeOptionDto>>($"api/prickproduction/allergens{query}");
        }

        public async Task<List<SimpleCodeOptionDto>> GetProductTypes(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<SimpleCodeOptionDto>>($"api/prickproduction/producttypes{query}");
        }

        public async Task<List<StockCheckDto>> StockCheck(string query)
        {
            return await dataService.Get<List<StockCheckDto>>($"api/prickproduction/stockcheck?query={Uri.EscapeDataString(query)}");
        }

        public async Task<List<PendingReceiptSummaryDto>> GetPendingSummary(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<PendingReceiptSummaryDto>>($"api/prickproduction/pending-summary{query}");
        }

        public async Task<ProductionOrderDto> CreateOrder(CreateProductionOrderRequest req)
        {
            return await dataService.Post<CreateProductionOrderRequest, ProductionOrderDto>("api/prickproduction/orders", req);
        }

        public async Task<List<ReceivingImportRowDto>> ParseReceivingExcel(byte[] fileBytes, string fileName)
        {
            return await dataService.PostFile<List<ReceivingImportRowDto>>(
                "api/prickproduction/parse-receiving-excel", fileBytes, fileName);
        }

        public async Task<ReceiveStockResult> ReceiveStock(ReceiveStockRequest req)
        {
            return await dataService.Post<ReceiveStockRequest, ReceiveStockResult>("api/prickproduction/receive", req);
        }
    }
}
