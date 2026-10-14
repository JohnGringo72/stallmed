using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class DailySummaryService
    {
        private readonly DataService dataService;

        public DailySummaryService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<DailySummaryDto?> GetDay(DateTime date)
            => await dataService.Get<DailySummaryDto>(
                $"api/dailysummary?date={date:yyyy-MM-dd}");
    }
}
