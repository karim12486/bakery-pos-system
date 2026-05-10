using BakeryPOS.API.Core.Entities;
using BakeryPOS.API.Core.Enums;
using BakeryPOS.API.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace BakeryPOS.API.Data.Seed
{
    /// <summary>
    /// First-run bootstrap. Idempotent — safe to call on every startup.
    /// <list type="number">
    ///   <item>Applies pending EF migrations.</item>
    ///   <item>Ensures a default Tenant + Branch exist (slug = "default"). This is what the
    ///         original freelance bakery customer's data gets migrated under when the SaaS
    ///         migration migration runs.</item>
    ///   <item>Ensures the default admin user exists, scoped to the default tenant, with a
    ///         cryptographically random password printed once to the console and a one-shot
    ///         credentials file.</item>
    /// </list>
    /// </summary>
    public static class DbInitializer
    {
        public const string DefaultTenantSlug = "default";

        public static async Task Initialize(
            AppDbContext context,
            IPasswordService passwordService,
            ILogger logger,
            string contentRootPath)
        {
            await context.Database.MigrateAsync();

            // 1. Default Tenant
            // IgnoreQueryFilters because the seeder runs without a tenant in scope.
            var tenant = await context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Slug == DefaultTenantSlug);

            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Name = "Default",
                    Slug = DefaultTenantSlug,
                    Plan = "trial",
                    Currency = "EGP",
                    Locale = "ar-EG",
                    Status = "active",
                    CreatedAt = DateTime.UtcNow
                };
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded default Tenant id={TenantId}", tenant.Id);
            }

            // 2. Default Branch under the default Tenant
            var branch = await context.Branches
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.TenantId == tenant.Id);

            if (branch == null)
            {
                branch = new Branch
                {
                    TenantId = tenant.Id,
                    Name = "Main",
                    Timezone = "Africa/Cairo",
                    TaxRate = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Branches.Add(branch);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded default Branch id={BranchId} for Tenant id={TenantId}",
                    branch.Id, tenant.Id);
            }

            // 3. Default admin user, scoped to the default tenant.
            var adminExists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Username == "admin");

            if (adminExists)
            {
                return;
            }

            var initialPassword = GenerateInitialPassword();

            var adminUser = new User
            {
                TenantId = tenant.Id,
                Username = "admin",
                PasswordHash = passwordService.HashPassword(initialPassword),
                FullName = "Default Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Permissions = UserPermissions.Admin
            };
            await context.Users.AddAsync(adminUser);
            await context.SaveChangesAsync();

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
            Console.WriteLine($"  Tenant:   {tenant.Slug}");
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
