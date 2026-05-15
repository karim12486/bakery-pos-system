using Nizam.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nizam.Api.Tests
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        // One in-memory DB per factory instance. xUnit creates one factory per test class
        // (via the SharedTestCollection fixture), so each test class gets its own DB.
        // Tests within a class share state — InitializeAsync per test does an
        // EnsureCreated + admin upsert which is idempotent.
        private readonly string _dbName = $"nizam-tests-{Guid.NewGuid():N}";

        // Static ctor runs ONCE per AppDomain, before any WebApplication.CreateBuilder is invoked.
        // Setting these as environment variables means the very first call to
        // WebApplication.CreateBuilder sees them via the default env-var configuration provider —
        // which is essential because Program.cs validates AppSettings:TokenKey at startup
        // (before WebApplicationFactory.ConfigureAppConfiguration callbacks are applied).
        static CustomWebApplicationFactory()
        {
            // Test-only key — must be >=64 bytes for HMAC-SHA512 validation in AuthenticationExtensions.
            Environment.SetEnvironmentVariable("AppSettings__TokenKey", new string('k', 64));
            Environment.SetEnvironmentVariable("AppSettings__TokenIssuer", "Nizam.Api");
            Environment.SetEnvironmentVariable("AppSettings__TokenAudience", "Nizam.Client");
            Environment.SetEnvironmentVariable("AppSettings__TokenLifetimeHours", "12");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // "Testing" is the gate that HangfireExtensions checks to skip SQL-backed
            // Hangfire registration + scheduling + dashboard. Without it, Hangfire's
            // RecurringJobManager.AddOrUpdate throws acquiring a SQL distributed lock
            // since there's no SQL Server in the test environment.
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Production registration in PersistenceExtensions is gated behind
                // !IsEnvironment("Testing"), so no SQL Server DbContext is registered.
                // We register the in-memory replacement here — EF Core's "only one provider
                // per service collection" rule means we couldn't co-register both anyway.
                services.AddDbContext<AppDbContext>(options => options
                    .UseInMemoryDatabase(_dbName)
                    // BeginTransactionAsync no-ops on the in-memory provider but logs a
                    // warning by default; suppress so service-layer transactional code runs
                    // unchanged under test.
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            });
        }
    }
}
