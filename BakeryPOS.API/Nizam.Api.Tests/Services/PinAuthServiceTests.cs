using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Entities;
using Nizam.Api.Core.Enums;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="PinAuthService"/> — set-PIN (format + per-tenant uniqueness),
/// PIN login (slug + PIN → token, generic failures), and the manager-override primitive.
/// </summary>
public class PinAuthServiceTests
{
    private const int TenantId = TestContextFactory.DefaultTenantId; // 1
    private const string Slug = "acme";

    private static readonly IPasswordService Passwords = new PasswordService();

    private static PinAuthService Build(AppDbContext ctx)
        => new(ctx, Passwords, new StubTokenService());

    private static async Task SeedTenantAsync(AppDbContext ctx)
    {
        ctx.Tenants.Add(new Tenant
        {
            Id = TenantId, Name = "Acme", Slug = Slug, PlanCode = "growth",
            BillingCycle = "monthly", Currency = "EGP", Locale = "ar-EG", Status = "active",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task<User> SeedUserAsync(
        AppDbContext ctx, string username, UserPermissions perms, string? pin = null, bool active = true)
    {
        var user = new User
        {
            Username = username, PasswordHash = "n/a", FullName = username,
            Permissions = perms, IsActive = active,
            PinHash = pin == null ? null : Passwords.HashPassword(pin),
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    // ----- SetPin -------------------------------------------------------------------

    [Fact]
    public async Task SetPin_ValidPin_Stores()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        var user = await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales);
        var svc = Build(ctx);

        await svc.SetPinAsync(user.Id, "1234", CancellationToken.None);

        var refreshed = await ctx.Users.FindAsync(user.Id);
        Assert.NotNull(refreshed!.PinHash);
        Assert.True(Passwords.VerifyPassword("1234", refreshed.PinHash!));
    }

    [Theory]
    [InlineData("123")]      // too short
    [InlineData("1234567")]  // too long
    [InlineData("12ab")]     // non-digit
    public async Task SetPin_InvalidFormat_Throws(string pin)
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        var user = await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales);
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.SetPinAsync(user.Id, pin, CancellationToken.None));
        Assert.Equal("ERR_INVALID_PIN", ex.ErrorCode);
    }

    [Fact]
    public async Task SetPin_DuplicateInTenant_Throws()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "1234");
        var bob = await SeedUserAsync(ctx, "bob", UserPermissions.ProcessSales);
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => svc.SetPinAsync(bob.Id, "1234", CancellationToken.None));
        Assert.Equal("ERR_PIN_TAKEN", ex.ErrorCode);
    }

    [Fact]
    public async Task SetPin_ChangingOwnPin_NotBlockedBySelf()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        var alice = await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "1234");
        var svc = Build(ctx);

        // Re-setting alice's PIN to the same value must not trip the uniqueness check (excludes self).
        await svc.SetPinAsync(alice.Id, "1234", CancellationToken.None);
        var refreshed = await ctx.Users.FindAsync(alice.Id);
        Assert.True(Passwords.VerifyPassword("1234", refreshed!.PinHash!));
    }

    // ----- PinLogin -----------------------------------------------------------------

    [Fact]
    public async Task PinLogin_Correct_ReturnsToken()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "4321");
        var svc = Build(ctx);

        var dto = await svc.PinLoginAsync(new PinLoginDto { TenantSlug = Slug, Pin = "4321" }, CancellationToken.None);
        Assert.Equal("alice", dto.Username);
        Assert.False(string.IsNullOrEmpty(dto.Token));
    }

    [Fact]
    public async Task PinLogin_WrongPin_Throws401()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "4321");
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PinLoginAsync(new PinLoginDto { TenantSlug = Slug, Pin = "0000" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
        Assert.Equal("ERR_PIN_LOGIN_FAILED", ex.ErrorCode);
    }

    [Fact]
    public async Task PinLogin_UnknownSlug_Throws401()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "4321");
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.PinLoginAsync(new PinLoginDto { TenantSlug = "nope", Pin = "4321" }, CancellationToken.None));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task PinLogin_InactiveUser_Throws401()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "alice", UserPermissions.ProcessSales, pin: "4321", active: false);
        var svc = Build(ctx);

        await Assert.ThrowsAsync<DomainException>(
            () => svc.PinLoginAsync(new PinLoginDto { TenantSlug = Slug, Pin = "4321" }, CancellationToken.None));
    }

    // ----- ValidateManagerPin -------------------------------------------------------

    [Fact]
    public async Task ValidateManagerPin_CorrectPinAndPermission_ReturnsUser()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "boss", UserPermissions.Admin, pin: "9999");
        var svc = Build(ctx);

        var manager = await svc.ValidateManagerPinAsync("9999", UserPermissions.Admin, CancellationToken.None);
        Assert.Equal("boss", manager.Username);
    }

    [Fact]
    public async Task ValidateManagerPin_CorrectPinWrongPermission_Throws403()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        // Cashier has a PIN but not the ManageUsers permission the override requires.
        await SeedUserAsync(ctx, "cashier", UserPermissions.ProcessSales, pin: "5555");
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.ValidateManagerPinAsync("5555", UserPermissions.ManageUsers, CancellationToken.None));
        Assert.Equal(403, ex.StatusCode);
        Assert.Equal("ERR_OVERRIDE_FORBIDDEN", ex.ErrorCode);
    }

    [Fact]
    public async Task ValidateManagerPin_WrongPin_Throws403()
    {
        using var ctx = TestContextFactory.Create();
        await SeedTenantAsync(ctx);
        await SeedUserAsync(ctx, "boss", UserPermissions.Admin, pin: "9999");
        var svc = Build(ctx);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => svc.ValidateManagerPinAsync("0000", UserPermissions.Admin, CancellationToken.None));
        Assert.Equal("ERR_OVERRIDE_PIN_INVALID", ex.ErrorCode);
    }

    // ----- Stub token service -------------------------------------------------------

    private sealed class StubTokenService : ITokenService
    {
        public string CreateToken(User user, int? branchId = null) => $"token-for-{user.Username}";
    }
}
