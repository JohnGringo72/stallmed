using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class PrickAllocationService
    {
        private readonly DataService dataService;

        public PrickAllocationService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<PendingOrderLineDto>> GetPending(string? company, int? doctorId)
        {
            var qs = new List<string>();
            if (!string.IsNullOrEmpty(company)) qs.Add($"company={company}");
            if (doctorId.HasValue) qs.Add($"doctorId={doctorId.Value}");
            var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
            return await dataService.Get<List<PendingOrderLineDto>>($"api/prickallocation/pending{query}");
        }

        public async Task<List<DoctorOptionDto>> GetDoctors(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<DoctorOptionDto>>($"api/prickallocation/doctors{query}");
        }

        public async Task<List<ActiveAllocationDto>> GetActive(string? company)
        {
            var query = string.IsNullOrEmpty(company) ? "" : $"?company={company}";
            return await dataService.Get<List<ActiveAllocationDto>>($"api/prickallocation/active{query}");
        }

        public async Task<AllocateResult> Allocate(AllocateRequest req)
        {
            return await dataService.Post<AllocateRequest, AllocateResult>("api/prickallocation/allocate", req);
        }

        public async Task<bool> Reverse(ReverseAllocationRequest req)
        {
            var result = await dataService.Post<ReverseAllocationRequest, object>("api/prickallocation/reverse", req);
            return true;
        }
    }
}
