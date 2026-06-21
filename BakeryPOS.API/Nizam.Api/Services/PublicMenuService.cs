using Nizam.Api.Common.Errors;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

public interface IPublicMenuService
{
    /// <summary>Resolves the public menu for a tenant by slug. Anonymous — no tenant context,
    /// so every query is explicitly tenant-scoped via IgnoreQueryFilters. Throws 404 if the
    /// tenant doesn't exist or its plan doesn't grant the <c>qr_table_menu</c> feature (we don't
    /// leak which of the two it is).</summary>
    Task<PublicMenuDto> GetMenuAsync(string tenantSlug, CancellationToken ct);
}

public sealed class PublicMenuService : IPublicMenuService
{
    private const string Feature = "qr_table_menu";

    private readonly AppDbContext _context;

    public PublicMenuService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PublicMenuDto> GetMenuAsync(string tenantSlug, CancellationToken ct)
    {
        var slug = (tenantSlug ?? string.Empty).Trim().ToLower();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.Status == "active", ct);
        if (tenant == null)
            throw new DomainNotFoundException("ERR_MENU_NOT_FOUND", "Menu introuvable.");

        // Plan gate (manual — this endpoint is anonymous, so [RequiresFeature] can't run).
        var hasFeature = await _context.PlanFeatures
            .IgnoreQueryFilters()
            .AnyAsync(f => f.PlanCode == tenant.PlanCode && f.FeatureKey == Feature, ct);
        if (!hasFeature)
            throw new DomainNotFoundException("ERR_MENU_NOT_FOUND", "Menu introuvable.");

        var tid = tenant.Id;

        var categories = await _context.Categories
            .IgnoreQueryFilters().Where(c => c.TenantId == tid)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        var products = await _context.Products
            .IgnoreQueryFilters().Where(p => p.TenantId == tid && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        // Product → its attached active modifier groups.
        var productIds = products.Select(p => p.Id).ToList();
        var links = await _context.ProductModifierGroups
            .IgnoreQueryFilters().Where(l => l.TenantId == tid && productIds.Contains(l.ProductId))
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .ToListAsync(ct);

        var groupIds = links.Select(l => l.ModifierGroupId).Distinct().ToList();
        var groups = await _context.ModifierGroups
            .IgnoreQueryFilters().Where(g => g.TenantId == tid && groupIds.Contains(g.Id) && g.IsActive)
            .ToListAsync(ct);
        var modifiers = await _context.Modifiers
            .IgnoreQueryFilters().Where(m => m.TenantId == tid && groupIds.Contains(m.ModifierGroupId) && m.IsActive)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .ToListAsync(ct);

        var groupsById = groups.ToDictionary(g => g.Id);
        var modsByGroup = modifiers.GroupBy(m => m.ModifierGroupId).ToDictionary(g => g.Key, g => g.ToList());
        var linksByProduct = links.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.ToList());

        PublicMenuModifierGroupDto MapGroup(int groupId)
        {
            var g = groupsById[groupId];
            return new PublicMenuModifierGroupDto
            {
                Id = g.Id, Name = g.Name, MinSelect = g.MinSelect, MaxSelect = g.MaxSelect,
                Modifiers = (modsByGroup.TryGetValue(groupId, out var ms) ? ms : new())
                    .Select(m => new PublicMenuModifierDto { Id = m.Id, Name = m.Name, PriceDelta = m.PriceDelta })
                    .ToList(),
            };
        }

        var productsByCategory = products.GroupBy(p => p.CategoryId).ToDictionary(g => g.Key, g => g.ToList());

        var menuCategories = categories
            .Where(c => productsByCategory.ContainsKey(c.Id)) // hide empty categories
            .Select(c => new PublicMenuCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Products = productsByCategory[c.Id].Select(p => new PublicMenuProductDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = string.IsNullOrEmpty(p.Description) ? null : p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    ModifierGroups = (linksByProduct.TryGetValue(p.Id, out var pls) ? pls : new())
                        .Where(l => groupsById.ContainsKey(l.ModifierGroupId)) // active groups only
                        .Select(l => MapGroup(l.ModifierGroupId))
                        .ToList(),
                }).ToList(),
            })
            .ToList();

        return new PublicMenuDto
        {
            TenantName = tenant.Name,
            Currency = tenant.Currency,
            Categories = menuCategories,
        };
    }
}
