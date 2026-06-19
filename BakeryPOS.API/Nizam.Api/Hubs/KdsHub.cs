using Nizam.Api.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nizam.Api.Hubs;

/// <summary>
/// Real-time Kitchen Display System hub. A KDS screen subscribes to one (branch, station)
/// group; the server pushes item lifecycle events (fired / cooking / ready / served / voided)
/// to that group as they happen.
///
/// <para><b>Group scoping</b>: <c>{tenantId}:kds:{branchId}:{stationId}</c> — tenant-scoped
/// so Tenant A's tickets never reach Tenant B, branch-scoped so the Maadi kitchen doesn't see
/// Zamalek's orders, station-scoped so the Bar screen only shows drinks. The tenant id comes
/// from the caller's JWT (never the client args) to prevent cross-tenant subscription.</para>
///
/// <para>Client → server: <see cref="JoinStation"/> / <see cref="LeaveStation"/>.
/// Server → client events (see <see cref="KdsEvents"/>): <c>ItemFired</c>, <c>ItemCooking</c>,
/// <c>ItemReady</c>, <c>ItemServed</c>, <c>ItemVoided</c> — each carries the updated item DTO.</para>
/// </summary>
[Authorize]
public class KdsHub : Hub
{
    /// <summary>Tenant + branch + station scoped group name. Used by the hub and the
    /// service's <c>IHubContext</c> broadcasts.</summary>
    public static string StationGroup(int tenantId, int branchId, int stationId)
        => $"{tenantId}:kds:{branchId}:{stationId}";

    /// <summary>Subscribe this connection to a (branch, station) KDS feed. Tenant comes from
    /// the JWT, not the args.</summary>
    public async Task JoinStation(int branchId, int stationId)
    {
        var tenantId = RequireTenant();
        await Groups.AddToGroupAsync(Context.ConnectionId, StationGroup(tenantId, branchId, stationId));
    }

    /// <summary>Unsubscribe from a (branch, station) feed.</summary>
    public async Task LeaveStation(int branchId, int stationId)
    {
        var tenantId = RequireTenant();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, StationGroup(tenantId, branchId, stationId));
    }

    private int RequireTenant()
    {
        var claim = Context.User?.FindFirst(CurrentTenant.TenantClaim)?.Value;
        if (!int.TryParse(claim, out var tenantId))
            throw new HubException("Tenant context missing.");
        return tenantId;
    }
}

/// <summary>Server→client event names broadcast on the KDS hub. Kept as constants so the
/// service and any client code agree on the wire contract.</summary>
public static class KdsEvents
{
    public const string ItemFired = "ItemFired";
    public const string ItemCooking = "ItemCooking";
    public const string ItemReady = "ItemReady";
    public const string ItemServed = "ItemServed";
    public const string ItemVoided = "ItemVoided";
}
