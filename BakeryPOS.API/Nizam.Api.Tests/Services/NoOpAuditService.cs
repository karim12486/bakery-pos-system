using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;

namespace Nizam.Api.Tests.Services;

/// <summary>
/// Test-only IAuditService that swallows everything. Audit-trail verification is a
/// separate concern from service-logic tests — services should be testable WITHOUT
/// caring whether the audit row was written. A future dedicated AuditServiceTests
/// covers the audit-writing path with a real AuditService + in-memory DbContext.
/// </summary>
internal sealed class NoOpAuditService : IAuditService
{
    public Task LogAsync(string action, string? entityType = null, int? entityId = null,
        string? details = null, CancellationToken ct = default) => Task.CompletedTask;

    public Task<PagedResponse<AuditLogDto>> ListAsync(AuditLogQuery query, PaginationParams pagination, CancellationToken ct) =>
        Task.FromResult(new PagedResponse<AuditLogDto>(Array.Empty<AuditLogDto>(), 1, pagination.PageSize, 0));
}
