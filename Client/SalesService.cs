using StallmedManager.Shared.Models;

namespace StallmedManager.Client
{
    public class SalesService
    {
        private readonly DataService dataService;

        public SalesService(DataService dataService)
        {
            this.dataService = dataService;
        }

        // ---- Επισκέψεις ----

        public async Task<List<SalesVisitViewDto>> GetVisits(DateTime from, DateTime to, int? userId, string? search)
        {
            var qs = $"?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
            if (userId.HasValue) qs += $"&userId={userId.Value}";
            if (!string.IsNullOrWhiteSpace(search)) qs += $"&search={Uri.EscapeDataString(search)}";
            return await dataService.Get<List<SalesVisitViewDto>>($"api/sales/visits{qs}") ?? new();
        }

        public async Task<SalesVisitViewDto?> GetLastVisit(int? doctorId, string? doctorName)
        {
            var qs = doctorId.HasValue
                ? $"?doctorId={doctorId.Value}"
                : $"?doctorName={Uri.EscapeDataString(doctorName ?? "")}";
            return await dataService.Get<SalesVisitViewDto?>($"api/sales/visits/last{qs}");
        }

        public async Task<SalesSaveResult?> SaveVisit(SaveSalesVisitRequest req)
            => await dataService.Post<SaveSalesVisitRequest, SalesSaveResult>("api/sales/visits/save", req);

        public async Task<SalesSaveResult?> DeleteVisit(DeleteSalesVisitRequest req)
            => await dataService.Post<DeleteSalesVisitRequest, SalesSaveResult>("api/sales/visits/delete", req);

        public async Task<List<SalesDoctorOptionDto>> SearchDoctors(string? search)
        {
            var qs = string.IsNullOrWhiteSpace(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
            return await dataService.Get<List<SalesDoctorOptionDto>>($"api/sales/doctors{qs}") ?? new();
        }

        public async Task<List<IssueUserDto>> GetUsers()
            => await dataService.Get<List<IssueUserDto>>("api/sales/users") ?? new();

        // ---- Εξοδολόγιο ----

        public async Task<SalesExpenseMonthDto?> GetExpenses(int actingUserId, int userId, int year, int month)
            => await dataService.Get<SalesExpenseMonthDto>(
                $"api/sales/expenses?actingUserId={actingUserId}&userId={userId}&year={year}&month={month}");

        public async Task<SalesSaveResult?> AddExpense(AddSalesExpenseRequest req)
            => await dataService.Post<AddSalesExpenseRequest, SalesSaveResult>("api/sales/expenses/add", req);

        public async Task<SalesSaveResult?> SaveExpenses(SaveSalesExpenseMonthRequest req)
            => await dataService.Post<SaveSalesExpenseMonthRequest, SalesSaveResult>("api/sales/expenses/save", req);
    }
}
