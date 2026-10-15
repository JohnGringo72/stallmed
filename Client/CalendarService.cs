using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class CalendarService
    {
        private readonly DataService dataService;

        public CalendarService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<List<CalendarEventDto>> GetEvents(int userId, DateTime from, DateTime to)
            => await dataService.Get<List<CalendarEventDto>>(
                $"api/calendar?userId={userId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}") ?? new();

        public async Task<CalendarSaveResult?> Save(SaveCalendarEventRequest req)
            => await dataService.Post<SaveCalendarEventRequest, CalendarSaveResult>("api/calendar/save", req);

        public async Task<CalendarSaveResult?> Delete(long eventId, int userId)
            => await dataService.Post<object, CalendarSaveResult>($"api/calendar/delete/{eventId}?userId={userId}", new { });
    }
}
