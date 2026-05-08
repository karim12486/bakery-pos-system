using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace BakeryPOS.API.Data.Seed
{
    public static class DbInitializer
    {
        public static async Task Initialize(
            AppDbContext context,
            IPasswordService passwordService,
            ILogger logger,
            string contentRootPath)
        {
            await context.Database.MigrateAsync();

            if (await context.Users.AnyAsync(u => u.Username == "admin"))
            {
                return;
            }

            var initialPassword = GenerateInitialPassword();

            var adminUser = new User
            {
                Username = "admin",
                PasswordHash = passwordService.HashPassword(initialPassword),
                FullName = "Default Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Permissions = UserPermissions.Admin
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

            // Surface the credential exactly once: console banner + a one-shot file
            // the operator is expected to read and delete.
            var credentialsFile = Path.Combine(contentRootPath, "INITIAL_ADMIN_PASSWORD.txt");
            try
            {
                File.WriteAllText(
                    credentialsFile,
                    $"Initial admin password: {initialPassword}{Environment.NewLine}" +
                    $"Generated: {DateTime.UtcNow:O}{Environment.NewLine}" +
                    "DELETE THIS FILE AFTER YOU HAVE LOGGED IN AND CHANGED THE PASSWORD." + Environment.NewLine);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not write initial admin password file at {Path}", credentialsFile);
            }

            const string banner = "==================================================";
            Console.WriteLine();
            Console.WriteLine(banner);
            Console.WriteLine("  INITIAL ADMIN ACCOUNT CREATED");
            Console.WriteLine($"  Username: admin");
            Console.WriteLine($"  Password: {initialPassword}");
            Console.WriteLine($"  (also written to: {credentialsFile})");
            Console.WriteLine("  >> Log in, change this password, then delete the file.");
            Console.WriteLine(banner);
            Console.WriteLine();

            logger.LogWarning(
                "Initial admin account seeded. Credentials written to {Path}. Change the password and delete the file.",
                credentialsFile);
        }

        // 18 random bytes -> 24 url-safe chars. ~143 bits of entropy.
        private static string GenerateInitialPassword()
        {
            Span<byte> bytes = stackalloc byte[18];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
