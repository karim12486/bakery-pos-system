using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerPayment> CustomerPayments { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<RemovalRequest> RemovalRequests { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Store enum as string for readability in the DB.
            modelBuilder.Entity<StockMovement>()
                .Property(s => s.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Report>()
                .Property(r => r.Type)
                .HasConversion<string>();

            modelBuilder.Entity<Sale>()
                .Property(s => s.PaymentMethod)
                .HasConversion<string>();

            modelBuilder.Entity<RemovalRequest>()
                .Property(r => r.Status)
                .HasConversion<string>();

            // Idempotency keys are unique per (endpoint, key). Once tenancy lands, add TenantId
            // to this composite index.
            modelBuilder.Entity<IdempotencyRecord>(e =>
            {
                e.Property(x => x.Key).HasMaxLength(80).IsRequired();
                e.Property(x => x.Endpoint).HasMaxLength(120).IsRequired();
                e.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.Endpoint, x.Key }).IsUnique();
            });
        }
    }
}