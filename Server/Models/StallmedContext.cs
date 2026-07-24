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
        public virtual DbSet<DoctorOrderAttachment> DoctorOrderAttachments { get; set; }
        public virtual DbSet<ShippingCourier> ShippingCouriers { get; set; }
        public virtual DbSet<StockReorderPoint> StockReorderPoints { get; set; }
        public virtual DbSet<DeliveryPerson> DeliveryPersons { get; set; }

        // ---- Quotes Module (Προσφορές) -- οι πελάτες/νοσοκομεία ζουν στον πίνακα Doctors ----
        public virtual DbSet<Quote> Quotes { get; set; }
        public virtual DbSet<QuoteLine> QuoteLines { get; set; }
        public virtual DbSet<QuoteEvent> QuoteEvents { get; set; }
        public virtual DbSet<QuoteAttachment> QuoteAttachments { get; set; }

        public StallmedContext(DbContextOptions<StallmedContext> options) : base(options)
        {
        }
    }
}
