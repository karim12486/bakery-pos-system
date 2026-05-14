# Architecture audit — bugs, inconsistencies, and risk register

**Date:** 2026-05-11
**Scope:** Every C# file on `feat/multi-tenant-foundation` (which stacks all prior work).
**Author:** post-implementation review acting as senior reviewer of own work.

Findings categorized by severity. **Numbers in [brackets]** are issue IDs used in fix-PR commit messages.

---

## 🔴 CRITICAL — security or correctness bugs. Fix before any production use.

### [C1] Global query filter defaults OPEN when tenant is null — silent cross-tenant data leak

**Where:** [AppDbContext.cs:98-111](BakeryPOS.API/BakeryPOS.API/Data/AppDbContext.cs#L98)
**The filter:**
```csharp
modelBuilder.Entity<Product>().HasQueryFilter(
    x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
```
**Bug:** When `_currentTenant.TenantId` is null, the predicate short-circuits to `true || ...` and the filter returns **every row across every tenant**. This is the WRONG default — null tenant should mean "show nothing", and cross-tenant access should require explicit `IgnoreQueryFilters()`.

**Why this breaks security:**
- A bug, a misconfigured auth middleware, or a malformed JWT that strips the `tenant_id` claim → tenant context becomes null → that request sees every tenant's data.
- Hosted services and the seeder currently rely on this behavior to access data across tenants. Both should use `IgnoreQueryFilters()` explicitly, which is auditable in code review.

**Fix:** Change every filter to closed: `x => x.TenantId == _currentTenant.TenantId`. Since `TenantId` is non-nullable `int` and `_currentTenant.TenantId` is `int?`, comparing them when the right side is null yields no rows in SQL — exactly the desired "show nothing" behavior. Then add `IgnoreQueryFilters()` to the specific paths that need cross-tenant access (login lookup, seeder, hosted services).

---

### [C2] `AuthService.LoginAsync` will return null for every user once C1 is fixed

**Where:** [AuthService.cs:42-43](BakeryPOS.API/BakeryPOS.API/Services/AuthService.cs#L42)
```csharp
var user = await _context.Users
    .SingleOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower(), ct);
```
**Bug:** Login happens BEFORE the user is identified — there's no `tenant_id` claim in `ICurrentTenant` yet. With the closed filter from [C1], this query returns null for every user. Login is broken under multi-tenancy.

**Fix:** Add `.IgnoreQueryFilters()`:
```csharp
var user = await _context.Users
    .IgnoreQueryFilters()
    .SingleOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower(), ct);
```

Same fix needed for `GetActiveUsernamesAsync` (kiosk picker) — though that endpoint should additionally be scoped by a tenant identifier (e.g., subdomain or installation hint) to avoid leaking all usernames across all tenants.

---

### [C3] `RemovalHub` SignalR group `"Admins"` is global — cross-tenant notification leak

**Where:** [RemovalHub.cs:12,21](BakeryPOS.API/BakeryPOS.API/Hubs/RemovalHub.cs#L12), [RemovalController.cs:57](BakeryPOS.API/BakeryPOS.API/Controllers/RemovalController.cs#L57)
**Bug:** Admins from any tenant join the single hub group `"Admins"`. When Tenant A's cashier raises a removal request, the controller fans out to that group — **every admin of every tenant receives the notification**, including the product name and price.

**Fix:** Tenant-scope the group name:
```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, $"Admins:{tenantId}");
await _hubContext.Clients.Group($"Admins:{tenantId}").SendAsync(...);
```
The hub needs access to `ICurrentTenant` (or read claims from `Context.User`).

**Additionally:** the cashier-targeted notification via `Clients.Client(cashierConnectionId)` is fine (specific connection id), but the controller should verify the cashier connection belongs to the same tenant as the admin responding — otherwise a malicious admin could spoof a `cashierConnectionId` from another tenant.

---

### [C4] Hosted services run without tenant context — they will see no data once C1 is fixed

**Where:**
- [ScheduledReportService.cs:46-99](BakeryPOS.API/BakeryPOS.API/Services/ScheduledReportService.cs#L46)
- [DatabaseBackupService.cs](BakeryPOS.API/BakeryPOS.API/Services/DatabaseBackupService.cs)

**Bug:** Both services create a service scope and resolve `AppDbContext`. The DbContext takes `ICurrentTenant` from the same scope. Outside an HTTP request, `IHttpContextAccessor.HttpContext` is null → `CurrentTenant.TenantId` is null → with closed filter ([C1]), queries return zero rows.

The current `ScheduledReportService` calls `IReportGenerationService` which (per its name) queries Sales/Products. Under closed filter: empty PDFs, no data, broken reports.

**Fix:**
- Iterate over all tenants: `var allTenants = await dbContext.Tenants.ToListAsync()`. For each, **scope a new DI scope with an `AmbientTenant(tenant.Id)` substituted for `ICurrentTenant`**, then generate that tenant's report.
- `DatabaseBackupService` runs `BACKUP DATABASE` (single-tenant SQL Server) — its raw SQL doesn't go through EF, so tenancy doesn't affect it. **However**, it backs up the entire shared multi-tenant DB to a single .bak file. This is fine when there's one host per tenant; problematic if NIZAM goes to a single shared deployment serving 100 tenants. Out of scope for [C4] but flag in operations doc.

---

### [C5] `RemovalController` mutates `RemovalRequest` cross-tenant via `FirstOrDefaultAsync(r => r.Id == requestId)`

**Where:** [RemovalController.cs:72-74](BakeryPOS.API/BakeryPOS.API/Controllers/RemovalController.cs#L72)
**Bug:** Once [C1] is fixed, this query is correctly scoped — but right now the request is looked up by id only. An admin from Tenant A passing a requestId belonging to Tenant B would mutate Tenant B's row (because filter is currently open under null tenant; with closed filter the row simply doesn't load and the controller returns NotFound — correct).

**Status:** Resolved as a side-effect of fixing [C1]. Flagged for tracking.

---

### [C6] `HasPermissionAttribute` doesn't `IgnoreQueryFilters` and could become broken in edge cases

**Where:** [HasPermissionAttribute.cs:55](BakeryPOS.API/BakeryPOS.API/Core/Attributes/HasPermissionAttribute.cs#L55)
**Bug:** Looks up user by username. Under the closed filter from [C1], the JWT's `tenant_id` claim must be valid AND match the user's TenantId. If the JWT was tampered with (different tenant_id), the lookup returns null → 401. **Actually correct behavior.** Issue only if `IHttpContextAccessor` isn't yet populated — but `IAuthorizationFilter` runs after authentication middleware, so it IS populated.

**Status:** SAFE under closed filter. Documented for clarity.

---

### [C7] `Customer.CurrentBalance` update is not atomic with the Sale creation in some paths

**Where:** [SalesService.cs:111](BakeryPOS.API/BakeryPOS.API/Services/SalesService.cs#L111), [CustomerService.cs:170](BakeryPOS.API/BakeryPOS.API/Services/CustomerService.cs#L170)
**Status:** SalesService now wraps in a transaction. CustomerService.RecordPaymentAsync also wraps. **Both OK.**

---

### [C8] `IdempotencyService.StoreAsync` race — concurrent stores can throw `DbUpdateException` on unique constraint

**Where:** [IdempotencyService.cs:19-29](BakeryPOS.API/BakeryPOS.API/Common/Idempotency/IdempotencyService.cs#L19)
**Bug:** If two requests with the same idempotency key arrive simultaneously, both pass `TryGetAsync` (returns null), both proceed to do work, both call `StoreAsync` — the second one throws `DbUpdateException` on the unique `(TenantId, Endpoint, Key)` index. Caller sees a 500.

**Fix:** Either:
- (a) Wrap the entire flow in a serializable transaction with `SELECT ... FOR UPDATE`-style locking — expensive.
- (b) Insert FIRST with placeholder values (lock the key), then do work, then update. Two writes; needs schema change.
- (c) Catch `DbUpdateException` on unique violation, retry `TryGetAsync` to fetch the winning response. Simplest; documented as "second writer loses". This is the pattern Stripe uses.

**Recommendation:** option (c). 3 lines of code.

---

## 🟠 HIGH — significant issues; fix soon.

### [H1] `PaymentType.Credit` semantic collision with project memory ("Credit" = card)

**Where:** [PaymentType.cs](BakeryPOS.API/BakeryPOS.API/Core/Enums/PaymentType.cs), [SalesService.cs:225-231](BakeryPOS.API/BakeryPOS.API/Services/SalesService.cs#L225)
**Project memory:** *"In Egyptian retail/F&B usage, 'Credit' is shorthand for any card payment — debit card, credit card, prepaid card. It does NOT mean store credit / customer tab."*
**Conflict:** `PaymentType.Credit` (used in the codebase) means "customer tab / debt", per [SalesService.SplitPayment](BakeryPOS.API/BakeryPOS.API/Services/SalesService.cs#L225). The UI button labeled "Credit" maps to `PaymentType.Card`.

**Risk:** Future developer (or me, in 6 months) sees `PaymentType.Credit` in code and assumes it's the UI button. Likely to misroute payment flows.

**Fix:** Rename `PaymentType.Credit` → `PaymentType.Tab` or `PaymentType.CustomerCredit`. This is a breaking enum change that needs a migration (the column stores it as string — see `Sale.PaymentMethod.HasConversion<string>()`). Existing rows with `"Credit"` need a backfill UPDATE.

---

### [H2] Password validation duplicated across DataAnnotations + FluentValidation

**Where:**
- [UserForCreationDto.cs](BakeryPOS.API/BakeryPOS.API/DTOs/UserForCreationDto.cs) — DataAnnotations + regex
- [ResetPasswordDto.cs](BakeryPOS.API/BakeryPOS.API/DTOs/ResetPasswordDto.cs) — DataAnnotations + regex
- [WriteDtoValidators.cs](BakeryPOS.API/BakeryPOS.API/DTOs/Validators/WriteDtoValidators.cs) — same rules in FluentValidation

**Bug:** Two validation pipelines run on every request. Both reject the same things. Duplicate maintenance — change a rule in one, forget the other.

**Fix:** Choose one. Recommendation: **remove all DataAnnotations from write DTOs and rely on FluentValidation only** (the modern path). Keep DataAnnotations for simple `[Required]` shape contracts on query DTOs.

---

### [H3] `Customer` hard-delete fails awkwardly when `CustomerPayment` rows exist

**Where:** [CustomerService.cs:149-156](BakeryPOS.API/BakeryPOS.API/Services/CustomerService.cs#L149)
**Bug:** Service checks for Sales existence but not CustomerPayments. A customer who has payment history but no sales (rare but possible) hits a `DbUpdateException` from the FK constraint instead of a domain error.

**Fix:** Add `_context.CustomerPayments.AnyAsync(p => p.CustomerId == id)` to the check, or switch to soft-delete (add `IsActive` to Customer entity).

**Recommendation:** soft-delete — preserves audit history across the board, consistent with how Users are deactivated.

---

### [H4] `InventoryController.AddStock` is not transaction-wrapped despite multi-write

**Where:** [InventoryController.cs:46-59](BakeryPOS.API/BakeryPOS.API/Controllers/InventoryController.cs#L46)
**Bug:** Updates `Product.StockQuantity` AND inserts `StockMovement` in one `SaveChangesAsync`. EF Core wraps multiple changes in a single transaction by default for `SaveChangesAsync`, so this is actually atomic — **but** the controller still does direct EF access and inline business logic, contrary to the service-layer pattern established for the other 5 controllers.

**Recommendation:** Extract `InventoryService` in a follow-up — modest LOC, aligns with the rest. Not a bug, just a consistency issue.

---

### [H5] `ImagesController` lacks tenant scoping, file-size limit, magic-number validation

**Where:** [ImagesController.cs](BakeryPOS.API/BakeryPOS.API/Controllers/ImagesController.cs)
**Bugs:**
- Uploaded files go to a SINGLE `wwwroot/images/` folder with no tenant prefix. Tenant A's product image is served at the same URL space as Tenant B's. If filenames collide (`Guid.NewGuid()` makes it unlikely but not impossible), risk of cross-tenant overwrite.
- No file size limit — a malicious client could upload a 10GB file.
- Extension-only validation. A file named `evil.jpg` with PHP/EXE content passes the check.
- `Request.Scheme` may be `http` behind a load balancer that terminates TLS — the saved URL would then be `http://...` and embed in receipts/UI as HTTP, mixed-content issues.

**Fix:** Move file uploads to:
- Tenant-prefixed paths: `wwwroot/images/tenant-{tenantId}/{uuid}.{ext}`
- Add `[RequestSizeLimit(5_000_000)]` on the endpoint.
- Validate magic numbers (file signature) for jpg/png/webp.
- Use `Request.Headers["X-Forwarded-Proto"]` (when behind a proxy) for scheme.

**Bigger picture:** the SaaS migration plan already calls for Azure Blob with tenant-prefixed keys. The local-disk path is a temporary mode for the bakery customer's on-prem install. Document and defer.

---

### [H6] `CategoriesController.DeleteCategory` doesn't check Product dependents

**Where:** [CategoriesController.cs:68-75](BakeryPOS.API/BakeryPOS.API/Controllers/CategoriesController.cs#L68)
**Bug:** Comment says *"Optional: Check if products exist in this category before deleting"*. It's not optional — without it, `Categories.Remove` throws `DbUpdateException` on the FK from Product.CategoryId.

**Fix:** Add `if (await _context.Products.AnyAsync(p => p.CategoryId == id)) throw new DomainConflictException(...)`.

---

### [H7] `DashboardController` 502 lines of inline EF + business logic + no service

**Where:** [DashboardController.cs](BakeryPOS.API/BakeryPOS.API/Controllers/DashboardController.cs)
**Status:** Intentionally deferred from PR-2b (read-only stats, EF filters handle tenancy automatically). But the size makes it hard to maintain and to test. Worth extracting in a focused refactor.

---

### [H8] CORS fallback is permissive in dev (any origin); no production warning

**Where:** [ApiExtensions.cs:18-36](BakeryPOS.API/BakeryPOS.API/Extensions/ApiExtensions.cs#L18)
**Bug:** When `Cors:AllowedOrigins` config is empty, the API allows ANY origin (without credentials). Fine for dev / LAN. **Dangerous for production.** Operators who forget to configure origins get an open API.

**Fix:** At startup, log a warning if running in `Production` environment AND `Cors:AllowedOrigins` is empty. Optionally refuse to start in `Production` without explicit CORS config.

---

### [H9] `Customer.CurrentBalance` sign convention is reversed from industry norm

**Where:** [Customer.cs:13](BakeryPOS.API/BakeryPOS.API/Core/Entities/Customer.cs#L13)
**Convention:** Positive = customer has store credit; Negative = customer owes money.
**Industry norm:** Positive = customer owes (Accounts Receivable); Negative = customer has prepaid credit.
**Risk:** Future accountant or integration engineer reads the sign wrong and shows debt as credit (or vice versa) in reports. Real money confusion.

**Fix:** Rename to `CreditBalance` and document explicitly, OR flip the sign convention and migrate the column. Flipping is a breaking change to existing data — needs a one-time UPDATE.

**Recommendation:** Document for now; flip if and when an accounting integration is added.

---

### [H10] `RemovalRequest.ProductName` is a string snapshot — denormalized but not labeled

**Where:** [RemovalRequest.cs](BakeryPOS.API/BakeryPOS.API/Core/Entities/RemovalRequest.cs)
**Status:** Intentional denormalization for audit history (the product may be renamed/deleted later). But the field has no XML doc explaining this. Future developer might add a `Product Product` navigation and "fix" the denormalization, losing audit fidelity.

**Fix:** Add XML doc explaining why this is intentional.

---

## 🟡 MEDIUM — code-quality, correctness in edge cases.

### [M1] All `FindAsync(id)` calls in tenant-scoped services should be `FirstOrDefaultAsync(x => x.Id == id)`

**Where:** 15 sites across `AdminService`, `CustomerService`, `ExpenseService`, `ProductService`, `SalesService`, plus `CategoriesController`, `InventoryController`, `ReportsController`.
**Status:** EF Core's `FindAsync` DOES apply query filters as of EF 5+. So under multi-tenancy these are SAFE.
**However:** FindAsync first checks the change tracker. If a cross-tenant entity was loaded earlier in the same scope (e.g., via `IgnoreQueryFilters()`), FindAsync returns it without going through the filter.

**Fix:** Replace with `FirstOrDefaultAsync(x => x.Id == id, ct)` — provably correct under the filter, no change-tracker fast-path ambiguity. Mechanical sed.

---

### [M2] Many nullable-annotation gaps (CS8618 warnings)

**Where:** Most entities (`Sale`, `SaleDetail`, `Product`, `StockMovement`, `Expense`, `CustomerPayment`, `RemovalRequest`) and several DTOs.
**Bug pattern:** `public User User { get; set; }` — non-nullable navigation that EF doesn't initialize until Include/Load.

**Fix:** Use the `required` modifier where the property is set by EF mapping, or make navigations nullable (`public User? User { get; set; }`). Per-entity decision.

---

### [M3] `ReportGenerationService` and `PdfGenerationService` not audited for tenancy

**Where:** [Services/ReportGenerationService.cs](BakeryPOS.API/BakeryPOS.API/Services/ReportGenerationService.cs), [Services/PdfGenerationService.cs](BakeryPOS.API/BakeryPOS.API/Services/PdfGenerationService.cs)
**Status:** I haven't read these yet in this audit. They almost certainly query Sales/Products and need to be tenant-aware.

**Fix:** Read both, verify they don't bypass filters via `IgnoreQueryFilters()` or `FromSqlRaw`.

---

### [M4] `RemovalController.RequestRemoval` doesn't validate `requestDto.CashierConnectionId`

**Where:** [RemovalController.cs:30-60](BakeryPOS.API/BakeryPOS.API/Controllers/RemovalController.cs#L30)
**Bug:** The cashier supplies their own SignalR connection id. A malicious cashier could supply ANOTHER cashier's connection id — the admin's approval would then be routed to the wrong person, who could approve/reject in their UI. Cross-cashier confusion.

**Fix:** Verify the connection id actually belongs to the requesting user via SignalR's user-id provider (or by tracking connection→userId in a dictionary).

---

### [M5] `DashboardController.GetSummary` uses `today = DateTime.UtcNow.Date` — branches in non-UTC tz see wrong "today"

**Where:** [DashboardController.cs:29](BakeryPOS.API/BakeryPOS.API/Controllers/DashboardController.cs#L29)
**Bug:** Cairo is UTC+2 (UTC+3 in DST? no, Egypt doesn't observe DST). A sale at 1 AM Cairo time on Tuesday gets recorded with `SaleDate = DateTime.UtcNow` = 23:00 Monday UTC. Today's-sales query for Cairo Tuesday would miss it.

**Fix:** Convert "today" to the branch's timezone (`Branch.Timezone`). Use `TimeZoneInfo.ConvertTimeFromUtc` consistently. Once Phase A's BranchId is required, this becomes natural.

---

### [M6] `ScheduledReportService` uses hardcoded `"Morocco Standard Time"` tz

**Where:** [ScheduledReportService.cs:34](BakeryPOS.API/BakeryPOS.API/Services/ScheduledReportService.cs#L34)
**Bug:** Bakery customer is Moroccan, hence the timezone. NIZAM launches Egypt — wrong tz. Also: hardcoded.

**Fix:** Use `Tenant.Locale`/`Branch.Timezone` per-tenant when iterating in [C4].

---

### [M7] `ScheduledReportService` doesn't iterate tenants — generates one set of reports against the (currently null) tenant context

**Where:** [ScheduledReportService.cs:46-99](BakeryPOS.API/BakeryPOS.API/Services/ScheduledReportService.cs#L46)
**Status:** Combined with [C4] — needs the same fix.

---

### [M8] `appsettings.Development.json` still leaks Telegram BotToken in repo history

**Where:** Earlier commits before the security branch.
**Status:** I stripped the values in `security/auth-hardening`. But they're still in `git log`. The user was advised to rotate.

**Action:** Confirm the user actually rotated the Telegram bot token. Otherwise still leaks.

---

### [M9] `JoinAdminsGroup` is called by clients but not by admins-only — anyone authenticated can subscribe to admin notifications

**Where:** [RemovalHub.cs:10-13](BakeryPOS.API/BakeryPOS.API/Hubs/RemovalHub.cs#L10)
**Bug:** Any authenticated user can call `JoinAdminsGroup()` and start receiving removal notifications, even if they don't have admin permissions.

**Fix:** Verify the caller has `ManageUsers` or `ApproveRemovals` permission before adding them to the group. Use `[Authorize]` with `Policy` or check `Context.User` claims inside the method.

---

### [M10] `Sale.PaymentMethod` is stored as a string but `PaymentType` enum changes are not migration-safe

**Where:** [AppDbContext.cs:42](BakeryPOS.API/BakeryPOS.API/Data/AppDbContext.cs#L42) — `Sale.PaymentMethod.HasConversion<string>()`
**Bug:** Renaming a `PaymentType` enum value (see [H1] — renaming `Credit` → `Tab`) corrupts existing rows that store `"Credit"` as their string value. EF won't auto-migrate this.

**Fix:** Provide a DB UPDATE migration alongside any enum rename.

---

### [M11] `ApiExtensions.AddBakeryPosCors` with credentials enabled + arbitrary origin would be a vulnerability (it currently disables credentials in the fallback path)

**Where:** [ApiExtensions.cs:24-35](BakeryPOS.API/BakeryPOS.API/Extensions/ApiExtensions.cs#L24)
**Status:** Correctly disables credentials in the permissive fallback. **OK.** Documented for clarity.

---

### [M12] `IPasswordService.GetHash` endpoint in `AuthController` is a password oracle

**Where:** [AuthController.cs:25-27](BakeryPOS.API/BakeryPOS.API/Controllers/AuthController.cs#L25)
**Status:** User explicitly said *"the gethash endpoint was just for testing only DW about it for now"*. Documented as known and accepted.

**Recommendation:** Remove or gate behind `IsDevelopment()` before any production deployment.

---

## 🟢 LOW — cosmetic / cleanup.

### [L1] AutoMapper 15.1.0 has a known high-severity vulnerability (NU1903)

**Where:** csproj. Build emits NU1903 warning.
**Fix:** Upgrade AutoMapper to a patched version (check NuGet for 15.x patches).

---

### [L2] Empty `Data/Repositories/` folder is scaffolding that was never used

**Where:** [BakeryPOS.API/BakeryPOS.API/Data/Repositories/](BakeryPOS.API/BakeryPOS.API/Data/Repositories/)
**Fix:** Delete the folder. The csproj `Folder Include="Data\Repositories\"` line should also go.

---

### [L3] Joke migration filenames `_xd` and `_xdddd`

**Where:** `Migrations/20251112214026_xd.cs`, `Migrations/20251122193623_xdddd.cs`
**Status:** Applied to the bakery customer's DB. Can't rename without dropping/recreating their DB.
**Fix:** Live with it; flag in onboarding doc.

---

### [L4] Hardcoded `fr-MA` culture in `WebApplicationExtensions.UseBakeryPosLocalization`

**Where:** [WebApplicationExtensions.cs:69-72](BakeryPOS.API/BakeryPOS.API/Extensions/WebApplicationExtensions.cs#L69)
**Status:** Already commented as "left over from the freelance bakery customer; SaaS migration replaces with per-tenant culture middleware".
**Fix:** Replace with middleware that reads `Tenant.Locale` once tenant context is reliable post-[C1]/[C2] fixes.

---

### [L5] `BakeryPos` extension method prefix vs NIZAM brand

**Where:** All extension methods in `Extensions/`.
**Status:** Internal naming; not customer-visible. Documented as a single sed-PR for later.

---

### [L6] `ReportsController.cs` has duplicate `using` directive

**Where:** [ReportsController.cs:2-3](BakeryPOS.API/BakeryPOS.API/Controllers/ReportsController.cs#L2)
```csharp
using BakeryPOS.API.Core.Interfaces;
using BakeryPOS.API.Core.Interfaces;
```
**Fix:** Remove the duplicate. One-line cleanup. Compiler emits CS0105 warning.

---

### [L7] `ScheduledReportService` writes PDF files to `GeneratedReports/` (process working dir)

**Where:** [ScheduledReportService.cs:15](BakeryPOS.API/BakeryPOS.API/Services/ScheduledReportService.cs#L15)
**Bug:** Relative path means the report folder is wherever the process happens to run. On the bakery customer's `.exe` install, this varies. Also: not tenant-scoped.
**Fix:** Use `Path.Combine(env.ContentRootPath, "GeneratedReports", $"tenant-{tenantId}")`.

---

### [L8] `JoinAdminsGroup` allows joining without specifying which admin role

**Where:** [RemovalHub.cs:10](BakeryPOS.API/BakeryPOS.API/Hubs/RemovalHub.cs#L10)
**Status:** Combined with [M9] / [C3].

---

### [L9] CSS encoding on every commit triggers LF→CRLF warnings

**Where:** every commit on this branch.
**Fix:** Add `.gitattributes` with `* text=auto` and `*.cs text eol=crlf` to enforce a consistent line-ending policy.

---

## What's NOT a bug (false positives I considered and ruled out)

- **FindAsync respects global query filters in EF 6+** — verified via docs. Listed as [M1] only because there's a change-tracker fast-path that bypasses the query (and thus the filter) if the entity was loaded with `IgnoreQueryFilters()` earlier in the same scope. Rare in practice.
- **The `_currentTenant` closure in HasQueryFilter** captures the AppDbContext instance, but reads `_currentTenant.TenantId` at query time (not at model-creation time). Correct pattern.
- **`SaveChanges` auto-stamping TenantId** is safe under the closed filter — only fires when `_currentTenant.TenantId.HasValue`, so the seeder and anonymous paths must stamp explicitly. Confirmed working.
- **`PaymentType.Split` payment math** in `SalesService.SplitPayment` — looks weird with the `cashPaid -= changeGiven` adjustment but is correct: when total tendered exceeds final amount, the surplus is returned as cash change, and we record `cashPaid` as actual revenue (tendered minus change).
- **`Sale.AmountOwed` rounding epsilon** (`> 0.001m`) — defensive against decimal float math. Documented as intentional.

---

## Fix sequencing — proposed PR breakdown

If you say "fix all", here's the minimal-risk order:

| PR | Issues | Risk |
|---|---|---|
| `security/closed-tenant-filter` | [C1], [C2], [C4] (login + hosted services + filter flip) | High — touches auth and reports |
| `security/scoped-removal-hub` | [C3], [M4], [M9] | Medium — SignalR change |
| `security/idempotency-race` | [C8] | Low |
| `refactor/findasync-to-firstordefault` | [M1] | Low — mechanical sed |
| `refactor/payment-type-rename` | [H1], [M10] | Medium — needs data migration |
| `refactor/validation-cleanup` | [H2] (DataAnnotations → FluentValidation only) | Low |
| `feat/customer-soft-delete` | [H3] | Low — schema change |
| `feat/category-delete-guard` | [H6] | Low |
| `refactor/images-tenant-scoped` | [H5] | Medium |
| `chore/audit-cleanup` | [L1]–[L9] cosmetic | Low |

Each PR is self-contained and reviewable. Total estimated work: 1–2 days for the critical/high ones, 2–3 days for the rest.

---

## Recommendation

**Stop and fix [C1]–[C4] before any more feature work.** The closed-filter migration is foundational — every subsequent PR that touches data goes through it. Doing it now is half a day; doing it after 30 more endpoints land is a week.

Then knock out [H1]–[H10] over a couple of days. The mediums and lows can be bundled into a `chore/audit-cleanup` PR.

Total: ~3–5 days of focused work to get the codebase to production-ready. Worth it before the SaaS migration goes any further.
