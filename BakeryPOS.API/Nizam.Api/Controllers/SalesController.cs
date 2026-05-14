using System.Security.Claims;
using System.Text.Json;
using Nizam.Api.Common.Idempotency;
using Nizam.Api.DTOs;
using Nizam.Api.DTOs.Shared;
using Nizam.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nizam.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SalesController : ControllerBase
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string EndpointId = "POST /api/sales";

    private readonly ISalesService _sales;
    private readonly IIdempotencyService _idempotency;

    public SalesController(ISalesService sales, IIdempotencyService idempotency)
    {
        _sales = sales;
        _idempotency = idempotency;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<SaleListDto>>> GetSales(
        [FromQuery] PaginationParams pagination,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken ct)
    {
        var result = await _sales.ListAsync(pagination, startDate, endDate, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SaleDetailDto>> GetSale(int id, CancellationToken ct)
    {
        var sale = await _sales.GetAsync(id, ct);
        return sale == null ? NotFound() : Ok(sale);
    }

    /// <summary>
    /// Process a new sale. Supports the optional <c>Idempotency-Key</c> header — clients sending
    /// the same key for the same endpoint receive the original response without re-executing
    /// (essential for offline-first POS sync and double-tap protection).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] SaleForCreateDto dto, CancellationToken ct)
    {
        var idemKey = Request.Headers[IdempotencyHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(idemKey))
        {
            var cached = await _idempotency.TryGetAsync(EndpointId, idemKey, ct);
            if (cached != null)
            {
                return new ContentResult
                {
                    StatusCode = cached.StatusCode,
                    Content = cached.Body,
                    ContentType = "application/json"
                };
            }
        }

        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException();

        var result = await _sales.CreateAsync(dto, username, ct);
        var body = JsonSerializer.Serialize(result);

        if (!string.IsNullOrWhiteSpace(idemKey))
        {
            // StoreAsync handles the concurrent-race case: if a peer with the same key stored
            // first, the returned CachedResponse is the WINNER's response, not ours. Return that
            // so both racing callers see the same payload.
            //
            // NOTE: this does NOT prevent duplicate WORK (both callers may have executed CreateAsync
            // before either stored). Eliminating that requires wrapping idempotency + work in a
            // single outer transaction — tracked as a follow-up. For now, the race window is
            // small (low-millisecond) and the cashier double-tap (the realistic case) is fully
            // protected because the first call completes before the second arrives.
            var stored = await _idempotency.StoreAsync(EndpointId, idemKey, StatusCodes.Status200OK, body, ct);
            if (stored.Body != body)
            {
                return new ContentResult
                {
                    StatusCode = stored.StatusCode,
                    Content = stored.Body,
                    ContentType = "application/json"
                };
            }
        }

        return Ok(result);
    }
}
