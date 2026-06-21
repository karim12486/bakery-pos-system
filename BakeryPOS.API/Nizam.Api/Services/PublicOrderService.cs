using Nizam.Api.Common.Errors;
using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Enums;
using Nizam.Api.Data;
using Nizam.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Services;

/// <summary>Payload for a guest order placed from the QR table menu.</summary>
public sealed class PublicOrderRequest
{
    public int TableId { get; set; }
    public AddOrderItemsDto Order { get; set; } = new();
}

public interface IPublicOrderService
{
    /// <summary>A seated guest submits items from the QR menu. They append to the table's open
    /// session order as Pending and the order is flagged customer-submitted, so staff review
    /// before firing. Anonymous — resolves the tenant by slug, then establishes the tenant scope
    /// so the normal dine-in service handles validation/pricing/routing.</summary>
    Task<DineInOrderDto> SubmitAsync(string tenantSlug, PublicOrderRequest request, CancellationToken ct);
}

public sealed class PublicOrderService : IPublicOrderService
{
    private const string Feature = "qr_table_menu";

    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDineInService _dineIn;

    public PublicOrderService(AppDbContext context, ICurrentTenant currentTenant, IDineInService dineIn)
    {
        _context = context;
        _currentTenant = currentTenant;
        _dineIn = dineIn;
    }

    public async Task<DineInOrderDto> SubmitAsync(string tenantSlug, PublicOrderRequest request, CancellationToken ct)
    {
        var slug = (tenantSlug ?? string.Empty).Trim().ToLower();

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.Status == "active", ct)
            ?? throw new DomainNotFoundException("ERR_MENU_NOT_FOUND", "Menu introuvable.");

        var hasFeature = await _context.PlanFeatures
            .IgnoreQueryFilters()
            .AnyAsync(f => f.PlanCode == tenant.PlanCode && f.FeatureKey == Feature, ct);
        if (!hasFeature)
            throw new DomainNotFoundException("ERR_MENU_NOT_FOUND", "Menu introuvable.");

        // Establish the tenant scope so every downstream query (and the dine-in service) is
        // correctly tenant-filtered — the anonymous request had no tenant context until now.
        if (_currentTenant is CurrentTenant ct2) ct2.SetOverride(tenant.Id);

        // The guest must already be seated — find the table's open session + its order.
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == request.TableId && t.IsActive, ct)
            ?? throw new DomainNotFoundException("ERR_TABLE_NOT_FOUND", "Table introuvable.");

        var session = await _context.TableSessions
            .FirstOrDefaultAsync(s => s.TableId == table.Id && s.ClosedAt == null, ct)
            ?? throw new DomainConflictException("ERR_NO_OPEN_SESSION",
                "This table has no open session. Please ask your server to start your order.");

        if (session.OrderId is not int orderId)
            throw new DomainConflictException("ERR_NO_OPEN_SESSION", "No order is open for this table.");

        // Append the guest's items via the normal flow (modifier validation + pricing + station
        // routing), then flag the order as customer-submitted for staff review.
        var result = await _dineIn.AddItemsAsync(orderId, request.Order, ct);

        var order = await _context.Orders.FirstAsync(o => o.Id == orderId, ct);
        if (!order.IsCustomerSubmitted)
        {
            order.IsCustomerSubmitted = true;
            await _context.SaveChangesAsync(ct);
        }

        return result;
    }
}
