using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PrickDoctorOrderService
    {
        private readonly DataService dataService;

        public PrickDoctorOrderService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<DoctorOrderViewDto>> GetOrders(string? company, int? doctorId, string? status)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={company}");
            if (doctorId.HasValue) qs.Add($"doctorId={doctorId.Value}");
            if (!string.IsNullOrEmpty(status)) qs.Add($"status={status}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<DoctorOrderViewDto>>($"api/prickdoctororder/orders{query}");
        }

        public async Task<List<DoctorOptionDto>> SearchDoctors(string? search)
        {
            var query = string.IsNullOrEmpty(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
            return await dataService.Get<List<DoctorOptionDto>>($"api/prickdoctororder/doctors{query}");
        }

        public async Task<DoctorOptionDto> QuickAddDoctor(QuickAddDoctorRequest req)
        {
            return await dataService.Post<QuickAddDoctorRequest, DoctorOptionDto>("api/prickdoctororder/doctors/quickadd", req);
        }

        public async Task<DoctorOrderViewDto> CreateOrder(CreateDoctorOrderRequest req)
        {
            return await dataService.Post<CreateDoctorOrderRequest, DoctorOrderViewDto>("api/prickdoctororder/orders", req);
        }

        public async Task<byte[]> DownloadTemplate()
        {
            return await dataService.GetBytes("api/prickdoctororder/import/template");
        }

        public async Task<ImportPreviewResult> ImportPreview(byte[] fileBytes, string fileName)
        {
            return await dataService.PostFile<ImportPreviewResult>("api/prickdoctororder/import/preview", fileBytes, fileName);
        }

        public async Task<int> ImportCommit(CommitImportRequest req)
        {
            return await dataService.Post<CommitImportRequest, int>("api/prickdoctororder/import/commit", req);
        }
    }
}
