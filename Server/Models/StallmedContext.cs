using Microsoft.EntityFrameworkCore;
using StallmedManager.Shared.Models;

namespace StallmedManager.Server.Models
{
    public class StallmedContext : DbContext
    {
        public virtual DbSet<Person> OnlineData { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<WebOrder> WebOrders { get; set; }

        public StallmedContext(DbContextOptions<StallmedContext> options) : base(options)
        {
        }
    }
}