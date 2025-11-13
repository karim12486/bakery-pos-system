using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Data.Seed
{
    public static class DbInitializer
    {
        public static async Task Initialize(AppDbContext context, IPasswordService passwordService)
        {
            // Apply any pending migrations
            await context.Database.MigrateAsync();

            // Check if the admin user already exists
            if (!await context.Users.AnyAsync(u => u.Username == "admin"))
            {
                // If not, create the admin user with a correctly hashed password
                var adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = passwordService.HashPassword("password"),
                    FullName = "Default Admin",
                    IsActive = true,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Permissions = UserPermissions.Admin
                };
                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }
}