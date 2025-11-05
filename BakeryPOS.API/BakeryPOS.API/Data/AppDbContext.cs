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

            // --- Seed the first Admin User ---
            var adminUser = new User
            {
                Id = 1, // Explicitly set the ID for seeding
                Username = "admin",
                // IMPORTANT: We will replace this with a real hash later
                PasswordHash = "$2a$11$j0.Uu8f2oXj0w/K0nQ9vQe2E.u2e/D9sU9vQ.f/gH3vR.p/gI5p/g", // Hash for "password"
                FullName = "Default Admin",
                IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Permissions = UserPermissions.Admin // Assign all permissions
            };

            modelBuilder.Entity<User>().HasData(adminUser);
        }
    }
}