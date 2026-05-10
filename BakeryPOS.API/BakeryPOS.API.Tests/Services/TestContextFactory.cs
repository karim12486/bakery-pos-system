using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Infrastructure.Internal;

namespace BakeryPOS.API.Tests.Services;

/// <summary>
/// Builds a fresh in-memory <see cref="AppDbContext"/> per test for service-level unit testing.
/// One DB name per test so parallel xUnit runs don't share state.
///
/// IMPORTANT: the in-memory provider doesn't enforce relational constraints (FKs, transactions,
/// concurrency). For tests that depend on those, use SQLite-in-memory or a real test DB instead.
/// </summary>
internal static class TestContextFactory
{
    /// <summary>Default tenant id used by tests when cross-tenant behaviour isn't being tested.</summary>
    public const int DefaultTenantId = 1;

    public static AppDbContext Create(
        int? tenantId = DefaultTenantId,
        [System.Runtime.CompilerServices.CallerMemberName] string testName = "")
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"nizam-test-{testName}-{Guid.NewGuid():N}")
            .EnableSensitiveDataLogging()
            // The in-memory provider doesn't support real transactions and warns on
            // BeginTransactionAsync. Services use transactions for correctness against
            // SQL Server; in tests we treat the call as a no-op.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new AmbientTenant(tenantId));
    }
}
