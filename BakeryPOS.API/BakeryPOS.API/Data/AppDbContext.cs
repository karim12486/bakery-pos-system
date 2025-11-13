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

        // We will add other DbSet properties here later for Products, Sales, etc.


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This line ensures the StockMovementType enum is stored as a string in the DB (e.g., "Sale", "Addition")
            // This makes the database much more readable than storing it as a number (0, 1, 2...).
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
        }
    }
}