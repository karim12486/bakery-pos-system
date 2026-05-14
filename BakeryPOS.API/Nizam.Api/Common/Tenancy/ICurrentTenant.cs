namespace Nizam.Api.Common.Tenancy;

/// <summary>
/// Request-scoped accessor for the tenant the current call belongs to. Implementations
/// resolve the tenant from the authenticated JWT's <c>tenant_id</c> claim.
///
/// <para>Why this exists:</para>
/// EF Core global query filters need a closure over the tenant id at <em>query</em> time,
/// not at <em>DbContext registration</em> time. The filter reads <see cref="TenantId"/> on
/// every query — which is the request-scoped value, so each request sees only its own data.
///
/// <para>Anonymous / cross-tenant operations:</para>
/// Background jobs, signup flow, and the seeder run outside any tenant — they get
/// <see cref="TenantId"/> = <c>null</c>. When the filter sees null it returns no rows
/// (safe-by-default). Such code paths must use <c>IgnoreQueryFilters()</c> explicitly when
/// they need cross-tenant access — that intent is auditable in code review.
/// </summary>
public interface ICurrentTenant
{
    /// <summary>Current tenant id, or null for anonymous / cross-tenant operations.</summary>
    int? TenantId { get; }

    /// <summary>Current branch id, or null when the request isn't branch-scoped (e.g. tenant-admin pages).</summary>
    int? BranchId { get; }

    /// <summary>True if a tenant is in scope (typical for authenticated requests).</summary>
    bool IsAuthenticated => TenantId.HasValue;
}
