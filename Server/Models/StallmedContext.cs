using Microsoft.EntityFrameworkCore;
using StallmedManager.Shared.Models;
namespace StallmedManager.Server.Models
{
    public class StallmedContext : DbContext
    {
        public virtual DbSet<Person> OnlineData { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<WebOrder> WebOrders { get; set; }

        // ---- Prick Test Stock Module ----
        public virtual DbSet<AllergenCode> AllergenCodes { get; set; }
        public virtual DbSet<ProductType> ProductTypes { get; set; }
        public virtual DbSet<Doctor> Doctors { get; set; }
        public virtual DbSet<DoctorOrder> DoctorOrders { get; set; }
        public virtual DbSet<DoctorOrderLine> DoctorOrderLines { get; set; }
        public virtual DbSet<ProductionOrder> ProductionOrders { get; set; }
        public virtual DbSet<ProductionOrderLine> ProductionOrderLines { get; set; }
        public virtual DbSet<StockReceipt> StockReceipts { get; set; }
        public virtual DbSet<StockTransaction> StockTransactions { get; set; }
        public virtual DbSet<OrderAllocation> OrderAllocations { get; set; }
        public virtual DbSet<ProductionReceiptAllocation> ProductionReceiptAllocations { get; set; }

        public StallmedContext(DbContextOptions<StallmedContext> options) : base(options)
        {
        }
    }
}