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
