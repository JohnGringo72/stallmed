using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class OrdersService
    {
        private DataService dataService;

        public OrdersService(DataService dataService)
        {
            this.dataService = dataService;
        }

        public async Task<WebOrder[]> GetOrders(
            DateTime fromDate,
            DateTime toDate,
            string filter,
            string doctor,
            string patient,
            string pharmacy,
            string status)
        {
            var url = $"/people?fromDate={fromDate:yyyy-MM-dd}" +
                      $"&toDate={toDate:yyyy-MM-dd}" +
                      $"&filter={Uri.EscapeDataString(filter ?? "")}" +
                      $"&doctor={Uri.EscapeDataString(doctor ?? "")}" +
                      $"&patient={Uri.EscapeDataString(patient ?? "")}" +
                      $"&pharmacy={Uri.EscapeDataString(pharmacy ?? "")}" +
                      $"&status={Uri.EscapeDataString(status ?? "")}";

            return await dataService.Get<WebOrder[]>(url) ?? Array.Empty<WebOrder>();
        }

        public async Task<OrderFilterOptions> GetFilterOptions()
        {
            return await dataService.Get<OrderFilterOptions>("/people/filter-options")
                   ?? new OrderFilterOptions();
        }

        public async Task<DoctorStats?> GetDoctorStats(string doctor, DateTime fromDate, DateTime toDate)
        {
            var url = $"/people/doctor-stats?doctor={Uri.EscapeDataString(doctor)}" +
                      $"&fromDate={fromDate:yyyy-MM-dd}" +
                      $"&toDate={toDate:yyyy-MM-dd}";
            return await dataService.Get<DoctorStats>(url);
        }
        public async Task<List<DoctorSummaryRow>> GetDoctorSummary(DateTime fromDate, DateTime toDate)
        {
            var url = $"/people/doctor-summary?fromDate={fromDate:yyyy-MM-dd}" +
                      $"&toDate={toDate:yyyy-MM-dd}";
            return await dataService.Get<List<DoctorSummaryRow>>(url) ?? new();
        }
        public async Task<CompanyStats?> GetCompanyStats(string company, string serverFilter, DateTime fromDate, DateTime toDate)
        {
            var url = $"/people/company-stats?company={Uri.EscapeDataString(company)}" +
                      $"&serverFilter={Uri.EscapeDataString(serverFilter ?? "")}" +
                      $"&fromDate={fromDate:yyyy-MM-dd}" +
                      $"&toDate={toDate:yyyy-MM-dd}";
            return await dataService.Get<CompanyStats>(url);
        }

        public async Task<List<TreatmentMixGroup>> GetTreatmentMixStats(DateTime fromDate, DateTime toDate, string company)
        {
            return await dataService.Get<List<TreatmentMixGroup>>(
                TreatmentMixUrl("treatment-mix-stats", fromDate, toDate, company)) ?? new();
        }

        public async Task<byte[]> DownloadTreatmentMixStatsExcel(DateTime fromDate, DateTime toDate, string company)
        {
            return await dataService.GetBytes(
                TreatmentMixUrl("treatment-mix-stats-excel", fromDate, toDate, company));
        }

        private static string TreatmentMixUrl(string action, DateTime fromDate, DateTime toDate, string company) =>
            $"/people/{action}?fromDate={fromDate:yyyy-MM-dd}" +
            $"&toDate={toDate:yyyy-MM-dd}" +
            $"&company={Uri.EscapeDataString(company ?? "")}";
    }
}
