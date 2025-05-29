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

		public async Task<Person[]> GetOrders(DateTime fromDate, DateTime toDate)
		{
			return await dataService.Get<Person[]>($"/people?fromDate={fromDate.ToString("yyyy-MM-dd")}&toDate={toDate.ToString("yyyy-MM-dd")}");
        }
	}
}