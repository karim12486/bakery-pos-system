using Nizam.Api.Common.Idempotency;
using Nizam.Api.Core.Entities;

namespace Nizam.Api.Tests.Services;

public class IdempotencyServiceTests
{
    [Fact]
    public async Task TryGet_ReturnsNull_WhenKeyNotStored()
    {
        await using var ctx = TestContextFactory.Create();
        var svc = new IdempotencyService(ctx);

        var result = await svc.TryGetAsync("POST /api/sales", "key-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task Store_ThenTryGet_ReturnsCachedResponse()
    {
        await using var ctx = TestContextFactory.Create();
        var svc = new IdempotencyService(ctx);

        await svc.StoreAsync("POST /api/sales", "key-2", 200, "{\"saleId\":42}");
        var result = await svc.TryGetAsync("POST /api/sales", "key-2");

        Assert.NotNull(result);
        Assert.Equal(200, result!.StatusCode);
        Assert.Equal("{\"saleId\":42}", result.Body);
    }

    [Fact]
    public async Task TryGet_IsScopedByEndpoint()
    {
        await using var ctx = TestContextFactory.Create();
        var svc = new IdempotencyService(ctx);

        await svc.StoreAsync("POST /api/sales", "shared-key", 200, "sale-body");
        var sameKeyOtherEndpoint = await svc.TryGetAsync("POST /api/customers", "shared-key");

        // Same key on a different endpoint must not collide — that's the whole point of the
        // composite unique index. Frontend can safely reuse a UUID across endpoints.
        Assert.Null(sameKeyOtherEndpoint);
    }
}
