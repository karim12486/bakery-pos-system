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

        // We will add other DbSet properties here later for Products, Sales, etc.


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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