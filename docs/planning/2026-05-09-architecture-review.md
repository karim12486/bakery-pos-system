# Architecture review — refactor before SaaS expansion?

**Date:** 2026-05-09
**Status:** RECOMMENDATION — refactor first. Three small PRs, ~1 week of work, saves much more than it costs once multi-tenancy lands. See §4 for the proposed cuts.

---

## 1. Verdict (TL;DR)

**Yes, refactor first.** The codebase isn't bad, it's *under-structured for what it's about to become*. Today it's a 12-controller, 2,400-LOC API talking directly to a single DbContext — a perfectly reasonable freelance bakery codebase. Tomorrow it's a multi-tenant SaaS with 3 frontends, an order-lifecycle FSM, and 50+ features. Without a foundation refactor, multi-tenancy code will smear across every controller, business logic will keep accreting in HTTP handlers, and Program.cs will hit 600 lines.

Three targeted PRs (~1 week solo) make the SaaS migration roughly 30–40% smaller and avoid a much-bigger refactor 6 months from now.

What's NOT broken: entity modelling, EF usage patterns, migrations, AutoMapper, the auth attribute, the SignalR hub structure, the DTO discipline. Plenty of decent foundations to build on.

---

## 2. The actual problems (ranked by how much pain they create when multi-tenancy lands)

### 2.1 Controllers do everything (CRITICAL)

`SalesController.CreateSale` is a 160-line method with: validation, customer lookup, product lookup, payment math (cash/card/split/credit branches), debt-balance updates, sale entity creation, stock decrement loops, and `SaveChangesAsync` — all inline, no transaction wrapping, no idempotency key.

`DashboardController` is 502 lines of inline EF queries with date math repeated in every endpoint.

Same shape across Customers (256), Sales (253), Expenses (224), Products (193), Admin (194).

**Why this hurts under multi-tenancy:**
- Every single one of these controllers needs `_context.X.Where(x => x.TenantId == currentTenantId)` added to every query. With ~50 EF calls across the codebase, that's 50 places to remember and 50 places a bug can hide.
- Branch scoping for sales/inventory needs the same treatment — another ~30 sites.
- Audit logging (required for SaaS trust) needs to wrap every write — another ~25 sites.

**What service-layer extraction buys us:**
- Tenant context lives in *one* place — a `ICurrentTenant` injected into services, applied by EF global query filters. Controllers don't know tenancy exists.
- Audit and validation get a single insertion point.
- Controllers shrink to <30 lines each (bind → call service → shape response).

### 2.2 Program.cs is a 205-line ball of wiring (HIGH)

CORS, EF, services, JWT, SignalR, Swagger, AutoMapper, hosted services, FileProvider, culture, hub mapping — all in one file. After my security work it grew further (rate limiter, secret validation). After multi-tenancy it'll grow again (TenantMiddleware, RLS connection interceptor, OutboxBackgroundService).

The empty `Extensions/` folder was clearly intended for this and never got used.

**Cost of fixing now:** 30 minutes — pure mechanical move into `services.AddNizamAuth()`, `AddNizamSwagger()`, `AddNizamRealtime()`, `AddNizamPersistence()`. Program.cs drops to ~40 lines.

### 2.3 No vertical-slice / feature folder structure (HIGH)

Today's layout is horizontal:
```
Controllers/    DTOs/    Services/    Mappers/    Migrations/    Core/Entities/
```

When you need to find "everything about Sales" you grep across 6 folders. When you add Orders + Tables + Modifiers + KDS for Phase B, you'll be grepping across 6 folders × N more entities.

**Vertical slice alternative** (one folder per feature):
```
Features/
  Sales/
    SalesController.cs
    CreateSaleHandler.cs
    SaleForCreateDto.cs
    SaleProfile.cs       (AutoMapper)
    SaleValidator.cs     (FluentValidation)
  Inventory/
  Menu/
  Auth/
  Tenancy/
  ...
Core/
  Entities/   (kept central — they're shared)
  Interfaces/
Infrastructure/
  Persistence/AppDbContext.cs
  Auth/TokenService.cs
  Notifications/Telegram*.cs
  Pdf/
```

Adding a new feature is one new folder. Removing one is one `rm -rf`. Code review by feature instead of by layer.

This refactor is **dramatically cheaper at 12 features than at 50**. Now is the moment.

### 2.4 No consistent error model (MEDIUM)

Today: `BadRequest("La vente doit contenir au moins un article.")`, `Conflict($"Stock insuffisant pour {product.Name}.")`, `Unauthorized("Caissier introuvable.")`, plus anonymous-object successes like `Ok(new { message, saleId, change })`.

Frontend has to handle each shape ad-hoc. SaaS support has no `errorCode` to grep on. Multi-language support means duplicating each French string in N more places.

**Fix:** adopt **ProblemDetails (RFC 7807)** with a custom extension for `errorCode`. One middleware turns thrown `DomainException`s into structured responses. Frontend gets one shape to parse. Localization keys replace inline French strings.

### 2.5 No structured logging (MEDIUM)

Default `ILogger<T>` only. No file sink, no cloud sink, no correlation IDs, no tenant-scoped log filtering.

For SaaS where support gets *"tenant X says checkout broke at 3pm"*, no-correlation-ID logs are useless. Adding **Serilog** with enrichers for `TenantId`, `BranchId`, `UserId`, `RequestId` is a one-time setup that pays back forever.

### 2.6 No API versioning (MEDIUM)

Routes are `/api/auth`, `/api/products`. No `v1`, no version header. The day the mobile app needs a v2 of the order endpoint while v1 is still in production for tablets, you're rewriting routes.

`Microsoft.AspNetCore.Mvc.Versioning` is one package and one `services.AddApiVersioning()` call. Then `[Route("api/v{version:apiVersion}/products")]` on every controller (mechanical sed).

### 2.7 Validation is sparse and DataAnnotations-only (MEDIUM)

`SaleForCreateDto`, `SaleDetailForCreateDto` — checked the file — have **no validation attributes at all**. The only validation is the manual `if (saleForCreateDto.SaleDetails == null || !saleForCreateDto.SaleDetails.Any())` inline.

Money endpoints with no validation = bug. Needs `FluentValidation` per DTO with rules like *"Quantity must be > 0"*, *"At least one detail"*, *"Card payment can't have split amounts"*.

### 2.8 No transactions on multi-write operations (MEDIUM-HIGH)

`SalesController.CreateSale` writes a `Sale`, N `SaleDetail`s, N `StockMovement`s, possibly updates `Customer.CurrentBalance`. If the DB hiccups halfway through `SaveChangesAsync`, you get a partial sale.

Wrap in `using var transaction = await _context.Database.BeginTransactionAsync(); ... await transaction.CommitAsync();`. Or move to a service method using `IDbContextFactory` with explicit transaction scope. Either way: today's behaviour is *"I hope nothing fails"*.

### 2.9 No idempotency keys (MEDIUM)

Cashier double-taps "Confirm Order" → two sales. POS network blip + retry → two sales. No `Idempotency-Key` header support. With offline-first POS in v1 (sync from queue with retries), this becomes critical.

Service layer is the right place to add it — check key in a small `IdempotencyRecord` table before processing.

### 2.10 Localization hardcoded (LOW for now, MEDIUM later)

`Program.cs:194` — `var defaultCulture = new CultureInfo("fr-MA");`. Comments above it list `en-US` and `ar-EG` alternatives that were never wired. After multi-tenancy, culture must be resolved from `Tenant.Locale` per-request.

Trivial fix once tenant context exists.

### 2.11 Things that are less-bad than I expected (FYI)

- `Customer.DiscountPercentage` already exists and is used in `SalesController.CreateSale` line 126. The "Premium discount tier" from the Figma is half-implemented already. My §8 entity sketch had this as new — actually existing.
- `PaymentType` enum already has `Cash`, `Card`, `Split`, `Credit`. The SaaS migration plan already aligns.
- Migrations folder has a couple of joke names (`_xd`, `_xdddd`) but they're applied to the customer's DB — leaving them alone.
- AutoMapper profiles file (70 LOC) is small enough today; will need splitting at ~150 LOC.

### 2.12 Things I'd skip (premature)

- **CQRS / MediatR** — the codebase isn't complex enough yet. Add if Phase B order-lifecycle gets nasty.
- **Multi-DbContext** — premature partitioning. One context is fine through Phase B.
- **Formal Repository pattern** — service layer + EF is enough. Repositories on top of EF are usually code-smell.
- **Health checks** — one-line addition, anytime.
- **Migration cleanup** — leave alone; would force a customer DB rebuild for cosmetic gain.

---

## 3. The "fix it later" tax

Concrete numbers for *not* refactoring first:

| Refactor | Cost now (clean codebase) | Cost after multi-tenancy lands |
|---|---|---|
| Service layer extraction | ~3 days, mechanical | ~7–10 days (must rethread tenant context through every controller) |
| Vertical-slice reorg | ~1 day, sed-and-move | ~4–5 days (more files, cross-references multiply) |
| Program.cs decomposition | 30 minutes | 1–2 hours (more middleware to migrate) |
| ProblemDetails migration | ~half day (12 controllers) | 1–2 days (50+ endpoints) |
| FluentValidation rollout | ~half day | 1–2 days |
| Serilog + correlation IDs | ~1 hour | ~half day (need backfill on existing logs) |
| API versioning | 30 min mechanical | ~half day (URL changes require coordinated FE deploy) |

Total now: **~5–6 days solo**.
Total after: **~3–4 weeks**.

Plus: every multi-tenancy bug found *after* the SaaS migration with the unrefactored code will be worse to debug (no service boundaries, no correlation IDs, no error codes).

---

## 4. Proposed refactor — 3 PRs, in order

### PR-1: `refactor/program-cs-extensions`
- Split Program.cs into `Extensions/ServiceCollectionExtensions.cs` (one method per concern: `AddNizamAuth`, `AddNizamPersistence`, `AddNizamRealtime`, `AddNizamSwagger`, `AddNizamRateLimiting`, `AddNizamObservability`).
- Move middleware ordering into `Extensions/WebApplicationExtensions.cs` (`UseNizamPipeline`).
- Program.cs ends up ~40 lines.
- **Add Serilog** here too (it's a Program.cs concern), with enrichers for `RequestId` and a placeholder for `TenantId`/`BranchId` (filled in by middleware once those exist).
- **Add Health checks** (`/health/ready`, `/health/live`) — one-liner.
- **Add API versioning** — register `AddApiVersioning`, prefix all controllers `[Route("api/v{version:apiVersion}/[controller]")]`.
- No behavioural change. Build + tests must still pass.

### PR-2: `refactor/feature-folders-and-services`
- Move from horizontal layout to vertical slices:
  ```
  Features/Auth/        (controller + handlers + DTOs + validators + profile)
  Features/Admin/
  Features/Products/
  Features/Categories/
  Features/Sales/
  Features/Inventory/
  Features/Customers/
  Features/Expenses/
  Features/Dashboard/
  Features/Reports/
  Features/Removal/
  Features/Images/
  Core/Entities/         (entities stay shared)
  Core/Interfaces/       (cross-cutting interfaces stay shared)
  Infrastructure/Persistence/AppDbContext.cs
  Infrastructure/Auth/   (TokenService, PasswordService)
  Infrastructure/Notifications/
  Infrastructure/Pdf/
  ```
- Introduce **service classes** per feature (e.g. `SalesService` with `CreateSaleAsync`, `GetSaleAsync`, `ListSalesAsync`). Controllers shrink to bind → call service → return result.
- Extract `SalesController.CreateSale` business logic into `SalesService.CreateAsync` — wrap in transaction, add idempotency key support hook (table + check; even if cashier doesn't send it yet, ready when offline-sync arrives).
- Extract similar logic from `DashboardController` into a `DashboardService` (especially the date-range math; centralise it).
- Add **FluentValidation** per DTO (start with money-related DTOs: SaleForCreateDto, SaleDetailForCreateDto, then expand).
- Add **ProblemDetails** middleware + a `DomainException` base type. Replace inline `BadRequest("...")` with `throw new DomainException("ERR_INSUFFICIENT_STOCK", "Stock insuffisant...")`. Middleware translates to RFC 7807 response with `errorCode` extension.
- Behaviour preserved end-to-end. All endpoints return same shape (or new RFC-7807 shape — frontend update needed).

### PR-3 (optional): `chore/test-skeleton`
- Existing tests are integration-style (ProductsControllerTests, CategoriesControllerTests, AdminControllerTests).
- Add unit-test scaffolding for the new services (xUnit + Moq + FluentAssertions, already in the test project's deps probably).
- Establish the pattern for "test the service, not the controller". One example test per service.
- Doesn't have to be exhaustive — just establish the pattern so later contributors follow it.

---

## 5. What this looks like end-to-end

Today's `SalesController.CreateSale` (160 lines) becomes:

**`Features/Sales/SalesController.cs`** (~25 lines):
```csharp
[HttpPost]
public async Task<ActionResult<SaleCreatedDto>> Create(SaleForCreateDto dto, CancellationToken ct)
{
    var result = await _salesService.CreateAsync(dto, ct);
    return Ok(result);
}
```

**`Features/Sales/SalesService.cs`** (~80 lines, with helper methods broken out):
```csharp
public async Task<SaleCreatedDto> CreateAsync(SaleForCreateDto dto, CancellationToken ct)
{
    using var tx = await _context.Database.BeginTransactionAsync(ct);

    var cashier = await _users.GetCurrentAsync(ct);
    var products = await _products.GetByIdsAsync(dto.SaleDetails.Select(d => d.ProductId), ct);
    var pricing = _pricingCalculator.Calculate(dto, products, cashier);
    await _stock.DecrementAsync(pricing.LineItems, ct);
    if (pricing.AmountOwed > 0) await _customers.IncreaseBalanceAsync(dto.CustomerId!.Value, pricing.AmountOwed, ct);

    var sale = pricing.ToSale(cashier);
    await _context.Sales.AddAsync(sale, ct);
    await _context.SaveChangesAsync(ct);
    await _audit.LogAsync(AuditAction.SaleCreated, sale, ct);
    await tx.CommitAsync(ct);

    return new SaleCreatedDto(sale.Id, pricing.ChangeGiven);
}
```

**`Features/Sales/SaleForCreateDtoValidator.cs`** (~15 lines, FluentValidation):
```csharp
RuleFor(x => x.SaleDetails).NotEmpty().WithErrorCode("ERR_NO_ITEMS");
RuleForEach(x => x.SaleDetails).SetValidator(new SaleDetailValidator());
RuleFor(x => x.CustomerId)
    .NotNull().When(x => x.PaymentMethod == PaymentType.Credit)
    .WithErrorCode("ERR_CUSTOMER_REQUIRED_FOR_CREDIT");
```

When multi-tenancy lands later, the change is to `_context.Sales.AddAsync(sale, ct)` only — the EF global filter handles the `WHERE TenantId = X` automatically. No tenant code in the controller, no tenant code in the service.

---

## 6. What I'm asking you to decide

Three options:

**(a) Do all three PRs first** (~5–6 days), then start the SaaS migration on a clean foundation. **My recommendation.** Highest payoff.

**(b) Do PR-1 only** (Program.cs + observability + versioning, ~1 day), defer PR-2/3 until Phase A is partly done. Compromise — prevents Program.cs becoming a monster, but you'll still pay the controller-fattening tax during multi-tenancy.

**(c) Skip the refactor**, start the SaaS migration immediately on the current shape. Fastest visible progress this week. Highest medium-term cost. Not what I'd do.

Pick A, B, or C. If A, I'll start with PR-1 immediately on a `refactor/program-cs-extensions` branch.

---

## 7. Risks of refactoring

Honest list:
- **Risk of breaking integration tests** during the move. Mitigation: verify build + tests at every step; commit frequently within the PR.
- **Risk of merge conflicts** if loay starts FE work against the current API shape. Mitigation: refactor doesn't change endpoint URLs or response shapes (except the optional ProblemDetails switch in PR-2 — and that one we'd coordinate).
- **Risk of scope creep** — refactoring is addictive. Mitigation: this doc is the fence. If a fix isn't listed, it's out of scope and goes on a backlog.
- **Risk of being a refactor-for-its-own-sake exercise** — fair. The only justification is that multi-tenancy specifically punishes the current shape. If we'd never go multi-tenant, I'd say leave it alone.

---

## 8. What does NOT change

- Solution structure (still .NET 9 ASP.NET Core API)
- Database schema (no migrations in this refactor)
- Auth model (still custom JWT, post-hardening)
- AutoMapper, EF Core, SignalR, QuestPDF — all stay
- The existing security work (`security/auth-hardening`) stays valid; will rebase or merge cleanly
- The decimal-quantity work (`feat/decimal-quantity-migration`) stays valid
- Existing tests continue to pass
- The Figma design implementation isn't blocked by any of this

---

## 9. If you say yes

Order of operations:

1. **Merge `chore/gitignore-planning`** to main (trivial, frees us from planning-docs-as-untracked-noise).
2. **Merge `security/auth-hardening`** to main (precondition — refactor on top of hardened code, not the other way).
3. **Merge `feat/decimal-quantity-migration`** to main (your WIP, your call when).
4. Branch `refactor/program-cs-extensions` off main → PR-1 → merge.
5. Branch `refactor/feature-folders-and-services` off main → PR-2 → merge.
6. Branch `chore/test-skeleton` off main → PR-3 (optional) → merge.
7. Branch `refactor/rename-to-nizam` off main → step 2 of the SaaS migration plan.
8. Continue with `feat/multi-tenant-foundation` etc. per the SaaS plan §11.

I'll start whenever you say go.
