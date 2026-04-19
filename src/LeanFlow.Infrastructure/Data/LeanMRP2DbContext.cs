using LeanFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeanFlow.Infrastructure.Data
{
    public class LeanMRP2DbContext : DbContext
    {
        public LeanMRP2DbContext(DbContextOptions<LeanMRP2DbContext> options) : base(options) { }

        public DbSet<RatingFile> RatingFiles { get; set; }
        public DbSet<InventoryRecord> InventoryRecords { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }
        public DbSet<DemandForecast> DemandForecasts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RatingFile>().HasKey(x => x.Id);
            modelBuilder.Entity<InventoryRecord>().HasKey(x => x.Id);
            modelBuilder.Entity<WorkOrder>().HasKey(x => x.Id);
            modelBuilder.Entity<DemandForecast>().HasKey(x => x.Id);
        }
    }
}
