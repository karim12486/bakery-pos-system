using Nizam.Api.Common.Errors;
using Nizam.Api.Core.Interfaces;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Nizam.Api.Services.Plans;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface ISuperAdminService
{
    Task<SuperAdminTokenDto> LoginAsync(SuperAdminLoginDto dto, CancellationToken ct);
    Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct);
    Task<TenantDetailDto> GetTenantAsync(int tenantId, CancellationToken ct);
    Task SetStatusAsync(int tenantId, string status, CancellationToken ct);
    Task ChangePlanAsync(int tenantId, string planCode, CancellationToken ct);
}

public sealed class SuperAdminService : ISuperAdminService
{
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    private readonly AppDbContext _context;
    private readonly IPasswordService _passwordService;
    private readonly ITokenService _tokenService;
    private readonly IPlanService _plans;

    public SuperAdminService(
        AppDbContext context, IPasswordService passwordService, ITokenService tokenService, IPlanService plans)
    {
        _context = context;
        _passwordService = passwordService;
        _tokenService = tokenService;
        _plans = plans;
    }

    public async Task<SuperAdminTokenDto> LoginAsync(SuperAdminLoginDto dto, CancellationToken ct)
    {
        var admin = await _context.SuperAdmins
            .FirstOrDefaultAsync(a => a.Username.ToLower() == dto.Username.ToLower(), ct);

        bool valid;
        if (admin != null) valid = _passwordService.VerifyPassword(dto.Password, admin.PasswordHash);
        else { _passwordService.VerifyPassword(dto.Password, DummyHash); valid = false; }

        if (admin == null || !admin.IsActive || !valid)
            throw new DomainException("ERR_SUPERADMIN_LOGIN_FAILED",
                "Identifiants invalides.", StatusCodes.Status401Unauthorized);

        return new SuperAdminTokenDto
        {
            Token = _tokenService.CreateSuperAdminToken(admin),
            Username = admin.Username,
            FullName = admin.FullName,
        };
    }

    public async Task<IReadOnlyList<TenantSummaryDto>> ListTenantsAsync(string? search, CancellationToken ct)
    {
        var query = _context.Tenants.IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(s) || t.Slug.Contains(s));
        }

        var tenants = await query.OrderBy(t => t.Name).ToListAsync(ct);
        var ids = tenants.Select(t => t.Id).ToList();

        // Counts in one pass each (cross-tenant).
        var branchCounts = await _context.Branches.IgnoreQueryFilters()
            .Where(b => ids.Contains(b.TenantId))
            .GroupBy(b => b.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var userCounts = await _context.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.TenantId) && u.IsActive)
            .GroupBy(u => u.TenantId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);

        var branchByTenant = branchCounts.ToDictionary(x => x.Key, x => x.Count);
        var userByTenant = userCounts.ToDictionary(x => x.Key, x => x.Count);

        return tenants.Select(t => ToSummary(t,
            branchByTenant.GetValueOrDefault(t.Id), userByTenant.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TenantDetailDto> GetTenantAsync(int tenantId, CancellationToken ct)
    {
        var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new DomainNotFoundException("ERR_TENANT_NOT_FOUND", $"Tenant {tenantId} introuvable.");

        var branchCount = await _context.Branches.IgnoreQueryFilters().CountAsync(b => b.TenantId == tenantId, ct);
        var userCount = await _context.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId && u.IsActive, ct);

        var overrides = await _context.TenantFeatureOverrides.IgnoreQueryFilters()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.FeatureKey)
            .Select(o => new TenantFeatureOverrideDto
            {
                FeatureKey = o.FeatureKey, Enabled = o.Enabled, ExpiresAt = o.ExpiresAt, Reason = o.Reason,
            })
            .ToListAsync(ct);

        return new TenantDetailDto
        {
            Tenant = ToSummary(tenant, branchCount, userCount),
            FeatureOverrides = overrides,
        };
    }

    public async Task SetStatusAsync(int tenantId, string status, CancellationToken ct)
    {
        if (status is not ("active" or "suspended"))
            throw new DomainException("ERR_INVALID_STATUS", "Status must be 'active' or 'suspended'.");

        var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new DomainNotFoundException("ERR_TENANT_NOT_FOUND", $"Tenant {tenantId} introuvable.");

        tenant.Status = status;
        await _context.SaveChangesAsync(ct);
    }

    public async Task ChangePlanAsync(int tenantId, string planCode, CancellationToken ct)
    {
        var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new DomainNotFoundException("ERR_TENANT_NOT_FOUND", $"Tenant {tenantId} introuvable.");

        if (!await _context.Plans.AnyAsync(p => p.Code == planCode && p.IsActive, ct))
            throw new DomainNotFoundException("ERR_PLAN_NOT_FOUND", $"Plan '{planCode}' introuvable ou inactif.");

        tenant.PlanCode = planCode;
        await _context.SaveChangesAsync(ct);
        _plans.InvalidateCache(tenantId); // new plan entitlements take effect immediately
    }

    private static TenantSummaryDto ToSummary(Core.Entities.Tenant t, int branchCount, int userCount) => new()
    {
        Id = t.Id, Name = t.Name, Slug = t.Slug, PlanCode = t.PlanCode, Status = t.Status,
        Currency = t.Currency, BranchCount = branchCount, UserCount = userCount, CreatedAt = t.CreatedAt,
    };
}
