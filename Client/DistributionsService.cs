using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class DistributionsService
    {
        private readonly DataService dataService;

        public DistributionsService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<Distribution>> GetList(string? item = null, int? doctorId = null)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(item)) qs.Add($"item={Uri.EscapeDataString(item)}");
            if (doctorId.HasValue) qs.Add($"doctorId={doctorId.Value}");
            var path = "api/distributions" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
            return await dataService.Get<List<Distribution>>(path) ?? new();
        }

        public async Task<List<string>> GetItems()
            => await dataService.Get<List<string>>("api/distributions/items") ?? new();

        public async Task<DistributionSaveResult?> Save(SaveDistributionRequest req)
            => await dataService.Post<SaveDistributionRequest, DistributionSaveResult>("api/distributions/save", req);

        public async Task<DistributionSaveResult?> Delete(DeleteDistributionRequest req)
            => await dataService.Post<DeleteDistributionRequest, DistributionSaveResult>("api/distributions/delete", req);
    }
}
