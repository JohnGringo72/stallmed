using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class QuotesService
    {
        private readonly DataService dataService;

        public QuotesService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<QuoteViewDto>> GetQuotes(string? company, string? status, int? customerId,
            DateTime? fromDate, DateTime? toDate, string? search)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={company}");
            if (!string.IsNullOrEmpty(status)) qs.Add($"status={status}");
            if (customerId.HasValue) qs.Add($"customerId={customerId.Value}");
            if (fromDate.HasValue) qs.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
            if (toDate.HasValue) qs.Add($"toDate={toDate.Value:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<QuoteViewDto>>($"api/quotes{query}") ?? new();
        }

        public async Task<QuoteViewDto> CreateQuote(SaveQuoteRequest req)
            => await dataService.Post<SaveQuoteRequest, QuoteViewDto>("api/quotes", req);

        public async Task<QuoteViewDto> UpdateQuote(long id, SaveQuoteRequest req)
            => await dataService.Put<SaveQuoteRequest, QuoteViewDto>($"api/quotes/{id}", req);

        public async Task<byte[]> GeneratePdf(long id, QuoteActionRequest req)
            => await dataService.PostBytes($"api/quotes/{id}/pdf", req);

        public async Task<byte[]> DownloadPdf(long id)
            => await dataService.GetBytes($"api/quotes/{id}/pdf");

        public async Task<QuoteActionResult> Send(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/send", req);

        public async Task<QuoteActionResult> Accept(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/accept", req);

        public async Task<QuoteActionResult> Reject(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/reject", req);

        public async Task<QuoteActionResult> Expire(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/expire", req);

        public async Task<QuoteActionResult> Reissue(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/reissue", req);

        public async Task<QuoteActionResult> Convert(long id, QuoteActionRequest req)
            => await dataService.Post<QuoteActionRequest, QuoteActionResult>($"api/quotes/{id}/convert", req);

        public async Task<QuoteActionResult> Delete(long id)
            => await dataService.Post<object, QuoteActionResult>($"api/quotes/{id}/delete", new { });

        public async Task<List<CustomerDto>> GetCustomers(string? search)
        {
            var query = string.IsNullOrEmpty(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
            return await dataService.Get<List<CustomerDto>>($"api/quotes/customers{query}") ?? new();
        }

        public async Task<CustomerDto> CreateCustomer(CustomerDto dto)
            => await dataService.Post<CustomerDto, CustomerDto>("api/quotes/customers", dto);

        public async Task<CustomerDto> UpdateCustomer(int doctorId, CustomerDto dto)
            => await dataService.Put<CustomerDto, CustomerDto>($"api/quotes/customers/{doctorId}", dto);

        public async Task<byte[]> DownloadImportTemplate()
            => await dataService.GetBytes("api/quotes/import/template");

        public async Task<QuoteImportPreviewResult> ImportPreview(string company, byte[] fileBytes, string fileName)
            => await dataService.PostFile<QuoteImportPreviewResult>($"api/quotes/import/preview?company={company}", fileBytes, fileName);

        public async Task<List<AttachmentDto>> GetAttachments(long quoteId)
            => await dataService.Get<List<AttachmentDto>>($"api/quotes/attachments/{quoteId}") ?? new();

        public async Task<bool> UploadAttachment(long quoteId, byte[] fileBytes, string fileName)
        {
            var result = await dataService.PostFile<object>($"api/quotes/attachments/{quoteId}", fileBytes, fileName);
            return true;
        }

        public async Task<byte[]> DownloadAttachment(long attachmentId)
            => await dataService.GetBytes($"api/quotes/attachments/file/{attachmentId}");

        public async Task DeleteAttachment(long attachmentId)
            => await dataService.Post<object, object>($"api/quotes/attachments/delete/{attachmentId}", new { });

        public async Task<List<QuoteProductOptionDto>> GetAllergens(string company)
            => await dataService.Get<List<QuoteProductOptionDto>>($"api/quotes/allergens?company={company}") ?? new();

        public async Task<List<QuoteProductTypeOptionDto>> GetProductTypes(string company)
            => await dataService.Get<List<QuoteProductTypeOptionDto>>($"api/quotes/producttypes?company={company}") ?? new();
    }
}
