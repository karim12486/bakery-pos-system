# NIZAM (SaaS migration) — planning, analysis & open questions

**Date:** 2026-05-09 (last updated after full Figma walk-through)
**Branch:** `planning/saas-migration`
**Status:** DRAFT v6 — Q2 (Karim's BE work: nothing started beyond current repo) and Q3 (Cashier "Credit" = card, any type) resolved. Q1 (wedge sentence) deferred pending team discussion. **Code work for Phase A is now unblocked.** See §11 for next concrete steps.

---

## 0. v4 changelog — what the user told us (2026-05-09)

**Facts now confirmed:**
- The current bakery POS was built for a **single freelance client** — one real install. It is *reference work*, not a constraint we have to carry forward. Free hand on rename, refactor, and breaking changes.
- **Target market is broad F&B: restaurants, cafés, bakeries, "and shops like that".** Not bakery-only.
- Anything bakery-specific in the codebase, copy, or design is fair game to remove or genericize.

**What this changes from v3:**

| v3 said | v4 says |
|---|---|
| Wedge = bakery/café counter-service vertical | Wedge needs rethinking — see §3 below. Vertical-bakery is off the table. |
| MVP = ship exactly what's designed | MVP is now an open question. The current design supports **café / QSR / bakery (counter-service)** only. **Restaurants need tables + KDS + modifiers**, none of which are in Figma. See §2 for the two real options. |
| "Manage and track all bakery expenses" copy is fine | Bakery-specific copy in the design (Expenses subtitle, sample categories like Boulangerie/Pâtisserie/Viennoiserie in Cashier) is now wrong. Flag for A to genericize. |
| Modifier groups → Phase 2 | Probably MVP if restaurants are in MVP. Open. |
| Tables / KDS → Phase 2 | Open — depends on §2 decision. |
| "How many existing customers?" was open | **Answered: one.** Means we can rename `BakeryPOS.API` → `Nizam.Api`, drop bakery-specific entities/seeds/fixtures with no migration burden. |

**Update 2026-05-09 (later in same chat):**
- Wedge sentence → deferred to team discussion (Karim taking the draft to the founders' chat). Doesn't block code.
- Karim's in-flight multi-branch BE work → **none**. Nothing started beyond what's in this repo. Free hand on the §8 entity model.
- Cashier "Credit" payment method → **= card** (any card type — debit, credit, prepaid). Common Egyptian usage. Customer-tab is a separate concept attached to a Premium customer with non-zero balance. Locked: `Order.PaymentMethod` enum starts with `Cash` + `Card`. Saved as project memory.

**Code work for Phase A is now unblocked.** §11 lists the next concrete steps in order.

---

**What does NOT change:**
- Multi-tenant architecture (shared DB + TenantId + EF global filters + RLS).
- Multi-branch as the active in-flight workstream.
- Auth lean (custom JWT + refresh + tenant/branch claims, PIN auth for cashier shift-open).
- Real-time = SignalR + Redis backplane.
- Frontend = React across all 3 apps.
- i18n = AR primary + EN, full RTL.
- Hosting = Azure UAE North.
- Billing = Paymob (EGP) for end-customer payments.
- Offline-first POS in v1.
- The in-flight `ChangeQuantityToDecimal` migration. By-weight / decimal qty is broadly useful — delis, butchers, bulk goods, restaurants charging by portion. Keep it.

---

## 1. Figma findings — the designed product is smaller and sharper than the spec

### 1.1 Brand & strategy
- **Brand: NIZAM** (نظام). Tagline EN: *"The intelligent platform for your business growth."* AR: *"ابدأ ورديتك في المكان الصحيح"* ("Start your shift in the right place"). Old name `شركة الستر الدولية` retired.
- **Plan label visible: ENTERPRISE.** Implies a tiered subscription model is planned.
- **Bilingual is first-class.** The Arabic Branch Select is a true RTL mirror: layout reflected, brand renders as نظام., step indicator reads `الخطوة ١ من ٢ — الفرع`. Not a translation overlay — a parallel layout. Confirms the design system is RTL-aware from day 1.
- **Team: 3 people.** K Karim (BE), L loay (FE), A (UI/UX).
- **Active in-flight work:** Multi-Branch Support BE (Karim, in progress), UI/UX (A, in progress), FE (loay, queued).
- **Named competitor: Foodics.** Founders openly questioning differentiation on the FigJam board. Stated wedges so far: customization + (historically) one-time pricing.
- **Founders' wishlist (FigJam):** Multi-Branch · Loyalty Points · Promotions Engine · Timed Events · Customer-facing menu · AI analyses.

### 1.2 The actual nav (Sidebar.jpg + Main.jpg)
The sidebar is intentionally tight. **5 modules + 2 footer items.** No surprise items hidden inside.

```
[avatar] ADMIN · Karim Mohamed
─────────────
1. Cashier      ← POS (the active one in design)
2. Dashboard
3. Premium      ← % discount tier per VIP customer (NOT loyalty points)
4. Products
5. Staff        ← user management
─────────────
   Expense
   Log out
```

**Conspicuously NOT in the nav:** Settings, Branches admin, Inventory (separate from Products), Reports (separate from Dashboard), Tables, KDS, Modifiers, Categories (lives as a button inside Products), Loyalty Points, Orders/History, Shifts management.

### 1.3 What each designed screen actually contains

| Screen | What it shows | What it does NOT have |
|---|---|---|
| **Login** | Brand split (dark/cream), "Open POS Shift" CTA, Email/Employee ID + Access Password, "Reset PIN?" link, Remember-employee, EN/AR toggle, "Enterprise" plan badge, security audit notice | No SSO / Google / Apple. PIN auth implied (Reset PIN). |
| **Branch Select** | "Which branch are you at today?" Step 1 of 2. 6 branch cards in 2-col grid (`Maadi`, `Zamalek`, `New Cairo`, `Heliopolis`, `Downtown`, `Nasr City` — all real Cairo districts), pagination, "No Branch Selected" dead-state CTA | No branch search. |
| **Opening Cash Float** | Step 2 of 2. Big EGP amount display, on-screen numpad (1-9, C, 0, ⌫), "Open Session" CTA, branch + datetime context on left, banner: "This amount will be verified against the drawer at the end of your shift." | No End-of-Shift / Close Cash Float counterpart screen exists. |
| **Cashier (POS)** | "Good morning, Ahmed Al-Hassan", Today's Orders + Revenue inline KPIs, Search + Carts (with badge showing parked-cart count), category tabs (`All 12`, `Boulangerie`, `Pâtisserie`, `Viennoiserie`, `Cakes`, `Drinks`, `Sandwiches`, `Seasonal`), product grid with photo + name + price + **piece count** + BEST SELLER badge + qty-in-cart badge + **SOLD OUT** state. Right rail: Order #1042, line items with qty+/–, Subtotal + **Tax 5%** + Total, **Cash / Credit** payment selectors, "Confirm Order · $26.15" CTA | No Card payment as a separate option. No discount apply. No modifier picker. No customer attach. No refund/void flow. No table/seat selector. |
| **Dashboard** | Greeting, Generate Report CTA, date filter, 3 KPI cards (Today's Sales + order count + WoW%, Total Clients + WoW%, Discounts Applied + count + WoW%), Sales Overview line chart (Week/Month/Year), Expense vs Revenue bar chart, Top Selling Products list, Cashier Performance list | No branch comparison view. No branch picker. |
| **Products** | Title + Category filter button + Search button, table: Stock / Photo / Name / Price / Status / Action, edit + delete icons, "New product" CTA (typo: "proudct"), pagination | No SKU/barcode column. No variants. No modifiers. No tax-per-product. No allergens. |
| **Staff (User Management)** | Title + Search, table: # / Name / Date Created / Role / Status / Action, roles visible: **Admin, Cashier** only, "New user" CTA, pagination | No Branch column → **branch-scoped roles are NOT designed yet.** No "Manager" role. No invite-by-email flow. |
| **Premium (Client Discounts)** | "Premium Client" with % badge, table: # / Name / Date Created / Discount / Status / Action, percentage discounts (5–20%), "New Client" CTA, "Next" CTA top-right | This is **flat % off per customer**, NOT a points-based loyalty program. |
| **Expenses** | "Manage and track all bakery expenses" (note: "bakery" baked into copy), 3 KPI cards (Total Expenses + WoW%, Transactions count, Categories count), table: Date / Description / Recorded by / Paid / Category (color chips: Utilities, Raw Material, Supplies), Add New Expense (green) + Category filter | No bulk import. No supplier link. |

### 1.4 Snapshots saved
All under `docs/planning/figma-snapshots/`. Source JPGs were exported by the user at `F:/Work/BakeryPOS/Reborn/`.

---

## 2. MVP scope — DECIDED: Option C (A → B)

**A is the launching position. B is the destination.** Ship A first to get to revenue and validate the SaaS economics; B follows immediately after with the proceeds funding the work.

- **Phase A — "Counter-service launch"** (8–12 weeks, 3 people). Ship exactly what's designed today, multi-tenant + multi-branch. Sell to bakeries, cafés, QSR (juice bars, koshary places, coffee shops, takeaway joints). Brand positioning: *"NIZAM — the F&B platform for Egypt, launching with cafés and bakeries first."* Honest, not boxed-in.
- **Phase B — "Full F&B"** (+3–4 months after A). Add tables/floor plan, modifier groups, KDS, proper order lifecycle (open → fired → served → closed → paid). Open to sit-down restaurants.
- **Total to feature parity with Foodics:** ~5–7 months — same as Option B alone, **but with revenue from month 3 onwards**, customer feedback shaping B, and shipped surface to test brand/positioning.

### 2.1 Architecting A for B — the discipline that makes this work

The trap that kills "ship A then B" plans: building A's data model in a way that forces a rewrite when B arrives. Avoidable. The rule:

> **Build A's features. Build B's schema.**

Concretely, A's MVP backend uses these B-aware shapes from day 1, with B-only features simply unused / null / empty until Phase B:

| Entity / shape | A behaviour | B behaviour | Why now |
|---|---|---|---|
| `Order` (replaces direct `Sale`) | Always opened + paid + closed in one screen action; status = `closed` immediately | Lifecycle: `open` → `fired` → `served` → `closed` → `paid` | Sale becomes a payment record on the Order. No rewrite when B exposes the lifecycle. |
| `Order.status` enum | Always `closed` | Full FSM | Avoids adding a column later. |
| `Order.channel` enum | Always `takeaway` (counter-service) | `dine_in` / `takeaway` / `delivery` | Avoids backfill. |
| `Order.tableId` (nullable FK) | Always `null` | FK to `Table` | Schema room only. |
| `OrderItem.modifiers` (JSON or join) | Always `[]` | Populated | Items have a place to put modifier choices in B. |
| `OrderItem.status` | Always `closed` | `pending` → `fired` → `served` | KDS can hook in without item-table migration. |
| `OrderItem.firedAt`, `servedAt` (nullable) | Always `null` | Set by KDS | Same reason. |
| `KitchenTicket` table | Doesn't exist | Created in B | Adding a new table later is fine; modifying existing item table is painful. **Skip in A.** |
| `Modifier` / `ModifierGroup` tables | Don't exist | Created in B | Same — purely additive in B. **Skip in A.** |
| `Table` / floor plan tables | Don't exist | Created in B | Same. **Skip in A.** |
| Tax model | Per-order flat (5% from design) | Per-item supported (modifiers can have own tax) | Store tax per `OrderItem.taxAmount` from day 1, computed from order rate in A. |
| Auth roles | `Admin`, `Cashier` (designed) | + `Manager`, `Server`, `Kitchen` | Role is a string column — adding values is free. |
| Real-time hubs | `OrderHub` (POS sync, parked carts) | + `KitchenHub` (KDS) | Adding a new hub later is trivial. |

**Cost of the discipline:** ~10–15% extra backend work in A (mostly column additions and an `Order` envelope around `Sale`). Saves a multi-week migration when B starts.

**Anti-trap:** do NOT add `Modifier`, `Table`, or `KitchenTicket` *tables* in A even though they're "B-aware". Adding tables later is cheap; adding columns to existing tables under load is expensive. Schema room means *columns and FKs on entities A already needs*, not pre-creating B's entire schema.

### 2.2 Parallel work during Phase A

A (designer) shouldn't be idle once she finishes the A design backlog (§2.3 below). She should start designing B in parallel: floor plan editor, table picker, modifier picker, KDS layout, modifier admin. That way Phase B starts with designs already in hand and loay/Karim aren't blocked the day A ships.

### 2.3 Design gaps A needs to close before Phase A can ship
Counterparts to existing screens — not new features, just missing halves:

1. **End-of-shift / Close Cash Float** screen (counterpart to Opening). Mandatory.
2. **Branches admin page** (currently only branch *select* exists, no CRUD).
3. **Branch-scoped role assignment** in Staff (Staff page has no Branches column).
4. **Settings page** (tax rate, currency, business name, receipt header/footer, language, brand logo).
5. **Z-report screen** (auto-generated at shift close).
6. **Sidebar branch indicator** (cashier can't tell which branch they're in).
7. **Cashier: customer attach + discount apply** (Premium clients have a discount, no UI to apply it).
8. **Cashier: refund / void**.
9. **Genericize bakery copy:** Expenses subtitle says "bakery expenses"; Cashier sample categories are Boulangerie/Pâtisserie/Viennoiserie. Make tenant-configurable, not branded.
10. **Self-serve signup wizard** (deferrable — manual onboarding for first 5 tenants is fine).

### Design gaps regardless of A vs B
The designer A needs to close these counterparts to existing screens before *any* MVP can ship. Not new features, just missing halves:

1. **End-of-shift / Close Cash Float** screen (counterpart to Opening). Mandatory.
2. **Branches admin page** (currently only branch *select* exists, no CRUD).
3. **Branch-scoped role assignment** in Staff (Staff page has no Branches column).
4. **Settings page** (tax rate, currency, business name, receipt header/footer, language, brand logo).
5. **Z-report screen** (auto-generated at shift close).
6. **Sidebar branch indicator** (cashier can't tell which branch they're in).
7. **Cashier: customer attach + discount apply** (Premium clients have a discount, no UI to apply it).
8. **Cashier: refund / void**.
9. **Genericize bakery copy:** Expenses subtitle says "bakery expenses"; Cashier sample categories are Boulangerie/Pâtisserie/Viennoiserie. Make these tenant-configurable, not branded.
10. **Self-serve signup wizard** (deferrable — manual onboarding for first 5 tenants is fine).

### Design gaps to close before this MVP can ship
Items that the designer A needs to add — not features I'm proposing, just the obvious counterparts to what's drawn:

1. **End-of-shift / Close Cash Float** screen (counterpart to Opening Cash Float). Mandatory: the shift can't close without it.
2. **Branches admin page** — the owner needs to add/rename/disable branches. Currently designed: branch *selection* but not branch *management*.
3. **Branch-scoped role assignment** in Staff. Today the table has just "Role" — needs "Branches" too (a cashier might work at Maadi only; a manager might cover Maadi+Zamalek).
4. **Settings page** — at minimum: business name, tax rate (currently hardcoded 5%), currency (EGP visible), receipt header/footer, language default, brand logo upload.
5. **End-of-shift Z-report** screen — auto-generated from shift close, printable.
6. **Sidebar branch indicator** — Karim can't tell which branch he's currently in. Add a small label under the user name.
7. **Cashier: discount apply** — Premium clients have a % discount, but no UI to attach a customer to the order. Could be in a customer/cart side-panel.
8. **Cashier: refund / void** — required by every real POS.
9. **Onboarding wizard** — referenced in the spec ("6-step"), but no screens drawn yet. Could be deferred if first 5 tenants are onboarded manually.

These should go on A's queue ahead of any new feature work.

### Disagreements with my v2 plan
v2 said *"loyalty in MVP, basic earn/redeem"*. **Wrong.** The design has Premium = flat % discount, not points. Loyalty points stays Phase 2.

v2 said *"add modifier groups in MVP"*. **Wrong.** Not designed; bakery's croissants don't really need modifiers. Defer.

v2 said *"add ingredients + recipes in MVP"*. **Out of MVP.** Not designed. Useful for COGS but Phase 2.

---

## 3. Strategic positioning vs Foodics — revised

With "bakery vertical" off the table, here are the wedges that survive for a broad-F&B SaaS:

| Wedge | Why it could work | Honest risk |
|---|---|---|
| **Egyptian-owned, EGP-billed, Arabic-first, local support** | Foodics is Saudi-headquartered. Local-first matters more in Egypt than people admit. EGP devaluation makes USD-priced SaaS painful. | Real but **not durable on its own** — needs to combine with another wedge. |
| **Counter-service-first, restaurants later** | Pairs with Option A in §2. Foodics is strong on full-service restaurants — weaker on the long tail of small cafés/bakeries/juice bars/koshary places that Egypt has thousands of. | Smaller TAM in year 1; story is harder to tell ("we don't do restaurants yet"). |
| **Customization** (founders' stated wedge) | Egypt SMEs do hate one-size-fits-all SaaS. Real differentiator for first 50 customers. | **Margin killer at scale.** Use as a sales motion for first cohorts, not a long-term product strategy. |
| **Price** (cheaper than Foodics) | Real lever in EGP-denominated SMEs hit by FX. | Race to the bottom; Foodics can match. Combine with another wedge or skip. |
| **Customer-facing menu page + QR ordering, included** | Lowers the cost of getting QR ordering live for any small F&B. | Several incumbents already do this; needs to be done well. |
| **AI-powered analyses** (founders' wishlist) | Could be a real wedge if it answers concrete questions ("which 3 products to discontinue", "should we raise the croissant price?"). | If it ships as "ChatGPT in a sidebar", it's a meme. Phase 3 at earliest. |

**My read** — given Phase A → Phase B is now the locked path, the wedge has two layers:

- **Phase A wedge (launch):** *Egyptian-first + small-F&B-friendly + meaningfully cheaper for SMB long-tail.* "We do cafés, bakeries, and quick-service better and cheaper than Foodics; restaurants coming Q3."
- **Phase B wedge (destination, after launch):** Same as above, plus *opinionated for how Egyptian operators actually run their places* (tabs/credit common, Arabic-first staff, EGP-only billing, WhatsApp not Telegram, integrates with local food-delivery apps like Talabat/Otlob).

Customization stays as a sales motion for the first 50 customers — useful to close deals, dangerous as a long-term identity. AI analyses parked for Phase 3.

This remains your call. But please write the wedge sentence on the FigJam — it becomes the tiebreaker for every future feature debate.

---

## 4. Personas (from spec, confirmed by design)

1. **Tenant admin / owner** — mobile-first; Dashboard + Reports.
2. **Branch manager** — *not yet a designed role*. Currently Staff knows only Admin/Cashier. Add at minimum a Manager role + branch assignment.
3. **Cashier** — designed for: tablet, "Open POS Shift" auth, parked carts, per-product piece-count visibility, BEST SELLER + SOLD OUT states.
4. **Kitchen staff** — *not in MVP*. KDS deferred to Phase 2.
5. **Super admin (your team)** — Phase 2. Internal portal.

---

## 5. Apps (3) — current Figma coverage

| App | Devices | Designed today | Verdict |
|---|---|---|---|
| Management portal | Web + responsive | Login, Dashboard, Premium, Products, Staff, Expenses | MVP-ready (post 2.x design gaps) |
| POS (Cashier) | 10–15" landscape tablet, touch | Login, Branch Select, Opening Cash Float, Cashier | MVP-ready (post 2.x design gaps) |
| KDS | Wall screen | **0 screens** | **Phase 2** |

---

## 6. Current code vs target — gap map (revised)

Only items relevant to the **MVP fence** are marked "MVP". Everything else is "Phase 2+".

| Capability | Current code | MVP / Phase 2+ | Notes |
|---|---|---|---|
| Multi-tenancy (TenantId everywhere) | None | **MVP** | The migration. |
| Multi-branch (Karim in flight) | None | **MVP** | Coordinate with Karim's BE work. |
| Tenant signup | Hardcoded admin seed | **MVP-lite** | Manual onboarding for first ~10 tenants is fine. Self-serve signup wizard = Phase 2. |
| Subscription / billing | None | **MVP-lite** | Manual invoicing → automate at tenant #6. |
| Roles + branch-scoped permissions | Bitflag perms, install-global | **MVP** | Add `UserBranchRole`. Designer needs to update Staff page. |
| Open POS Shift + opening cash float | None | **MVP** | Designed. |
| End-of-shift / close + Z-report | None | **MVP** | Not yet designed. Block on A. |
| Parked carts | None | **MVP** | Designed (badge in Cashier). |
| Menu: categories + products | ✅ exists | **MVP** | Add TenantId + BranchId. |
| **By-weight items** (decimal qty) | In-flight `ChangeQuantityToDecimal` | **MVP** | Finish that migration. |
| Modifier groups | None | **Phase 2** | Not designed. |
| Branch pricing overrides | None | **Phase 2** | Not designed. |
| Menu availability schedule | None | **Phase 2** | Not designed. |
| Ingredients + recipes / COGS | None | **Phase 2** | Not designed. |
| Inventory `StockMovement` | ✅ exists | **MVP** | Add TenantId + BranchId. POS surfaces piece-count per product. |
| Sales / payments cash + credit | ✅ exists | **MVP** | Designed. Need to confirm: where is Card? Cashier shows only Cash + Credit. |
| Premium (% discount per customer) | Customer entity exists | **MVP** | Just add a `DiscountPercent` field. |
| Customer credit (shop tabs) | ✅ exists | **MVP** | Keep. |
| Loyalty points | None | **Phase 2** | Not designed. Founders' wishlist but not an MVP screen. |
| Promotions engine | None | **Phase 2** | Not designed. |
| Customer-facing menu page (QR) | None | **Phase 2** | Not designed. |
| Tables / floor plan | None | **Phase 2** | Counter-service wedge. Not designed. |
| KDS / KOTs | None | **Phase 2** | Not designed. |
| Real-time | ✅ SignalR | **MVP** | Add Redis backplane. |
| Reports + PDF | ✅ QuestPDF | **MVP** | Scope per tenant + branch. Dashboard "Generate Report" CTA already designed. |
| Telegram per-tenant | Single bot | **Phase 2** | Replace with per-tenant channels. WhatsApp later. |
| File storage | wwwroot/images on disk | **MVP** | Move to Azure Blob, tenant-prefixed keys. |
| Background jobs | `IHostedService` per process | **MVP** | Hangfire on same SQL DB. |
| Localization | `fr-MA` hardcoded | **MVP** | `ar-EG` primary + `en`, full RTL. |
| Settings page | None | **MVP** | Not designed. Block on A. |
| Branches admin page | None | **MVP** | Not designed. Block on A. |
| Audit log | None | **MVP** | Required for SaaS trust. |

---

## 7. Architectural lean (carried forward)

| Area | Lean |
|---|---|
| Tenancy | Shared DB + `TenantId` everywhere + EF Core global query filters + SQL RLS as belt-and-braces |
| Hosting | Azure (UAE North or France Central) |
| Frontend | React across all three apps. Next.js (portal), Vite PWA + Tauri (POS, for thermal printer + offline), plain Vite (KDS — Phase 2). Shared design-system package. |
| Auth | Keep custom JWT. Add refresh tokens, tenant + branch + role claims. **PIN-based "Open POS Shift"** for cashiers. Email/password for owners/managers. |
| Real-time | SignalR + Redis backplane. |
| Background jobs | Hangfire on the same SQL DB. |
| Storage | Azure Blob, tenant-prefixed keys. |
| Billing | Paymob (EGP); Stripe for international tenants when relevant. |
| Offline-first POS | v1 requirement. IndexedDB queue + idempotent server endpoints keyed by client UUIDs. |
| i18n | Arabic primary + English. Full RTL. Western digits in receipts. |

---

## 8. Domain model — additions on top of current entities

```
Tenant            (id, name, plan, currency, locale, status, createdAt, …)
Subscription      (tenantId, plan, status, periodStart, periodEnd, paymentRef, …)
Branch            (id, tenantId, name, address, timezone, taxRate, currency, …)
TenantUser        (id, tenantId, name, email, phone, pinHash, …)   ← extends current User; PIN for shift-open
UserBranchRole    (userId, branchId, role)                          ← branch-scoped roles
Shift             (id, branchId, userId, openedAt, closedAt, openingFloat, closingCount, expectedCash, variance, …)
ParkedCart        (id, branchId, shiftId, name, items[], createdAt) ← supports the "Carts" feature in cashier UI
Setting           (tenantId, key, value)                            ← business name, tax %, receipt template, …
AuditLog          (id, tenantId, branchId, userId, action, entity, entityId, before, after, at)
```

**Existing entities to extend with TenantId (and BranchId where relevant):**
`Customer`, `Category`, `Product`, `StockMovement`, `Expense`, `ExpenseCategory`, `Sale`, `SaleDetail`, `CustomerPayment`, `Report`, `RemovalRequest`.

**New field on `Customer`:** `DiscountPercent decimal` for the Premium discount tier.

**Drop from MVP:** `LoyaltyAccount`, `LoyaltyTransaction`, `Modifier`, `ModifierGroup`, `BranchPriceOverride`, `MenuAvailability`, `Ingredient`, `RecipeItem`. All Phase 2+.

The in-flight `ChangeQuantityToDecimal` migration (visible in `git status` on `main`) is by-weight items — keep it.

---

## 9. Phasing — final cut

### MVP (target: 8–12 weeks, 3 people)
**= what's in the Figma today, made multi-tenant + multi-branch, with the design gaps in §2 closed.**

Backend (Karim):
1. `Tenant` + `Branch` + tenancy filter pipeline
2. `Shift` + Open/Close + Z-report
3. `ParkedCart`
4. `UserBranchRole` + auth refactor for tenant/branch claims, PIN auth path for cashier
5. `Setting`, `AuditLog`
6. Add `TenantId` + (where applicable) `BranchId` to existing entities + EF global filters
7. Finish `ChangeQuantityToDecimal` migration and merge
8. Move file uploads to Azure Blob with tenant-prefixed paths
9. Hangfire for scheduled reports / backups
10. Per-tenant scoped reports

UI/UX (A) — design backlog in priority order:
1. End-of-shift / Close Cash Float
2. Branches admin page
3. Branch-scoped role assignment in Staff
4. Settings page
5. Z-report screen
6. Sidebar branch indicator
7. Cashier: customer attach + discount apply
8. Cashier: refund / void
9. Self-serve signup wizard (can defer)

Frontend (loay):
1. React monorepo: `apps/portal`, `apps/pos`, `packages/design-system`, `packages/api-client`
2. Auth + tenant/branch context provider
3. RTL theming infrastructure (logical-properties CSS, dir="rtl" toggle)
4. Build out designed screens against API
5. Offline-first POS: IndexedDB queue, optimistic UI, idempotent sync
6. Thermal printer integration (USB via Tauri, BT later)

### Phase 2
- Modifier groups, branch pricing overrides, menu availability schedule
- Ingredients, recipes, COGS
- Purchase orders, suppliers, branch-to-branch transfers, waste log
- Loyalty points + promotions engine + timed events
- Customer-facing menu page (QR)
- Shift scheduling + clock-in/out
- Self-serve signup wizard + automated billing (Paymob)
- Super-admin internal portal
- WhatsApp notifications

### Phase 3
- KDS / tables / order lifecycle (only if expanding from counter-service)
- AI-powered analyses
- Native mobile owner app
- Multi-currency (GCC expansion)
- Public API + integrations marketplace

---

## 10. Open questions (revised — 1 left, non-blocking for code)

1. **Wedge sentence — TODO with team.** Karim is taking my draft (*"NIZAM is the Egyptian-first F&B platform that does cafés/bakeries/QSR better and cheaper than Foodics today, and restaurants by Q3."*) to the founders' chat. Doesn't block code; does block marketing/positioning. Lock before launch.

### Resolved
- ~~MVP scope (A vs B)~~ → **Option C: A → B.** A first (8–12 weeks), B follows with B-aware schema in A. See §2.
- ~~Existing customer count~~ → 1. Free hand on rename/refactor.
- ~~NIZAM brand locked?~~ → yes (FigJam "Done" column). Rename happens in the first migration commit.
- ~~Spec completeness~~ → moot. Figma is source of truth.
- ~~Bakery as the wedge~~ → off the table per Karim's clarification.
- ~~Karim's in-flight multi-branch BE work~~ → **nothing started beyond what's in this repo.** Free hand on the §8 entity model — no integration needed, no risk of collision.
- ~~Cashier "Credit" semantics~~ → **= card** (Visa/Mastercard/debit/whatever — Egyptian colloquial usage). Locked: `Order.PaymentMethod` enum starts with `Cash` and `Card`. Customer-tab is a separate concept, attached to a Premium customer with non-zero balance — not a payment method. (Saved as a project memory so future sessions don't re-litigate.)

### Notes / lower-priority items I'll just decide myself unless you object
- **Tax 5% in design:** treating as a placeholder. Will make tax rate per-branch in Settings (Egypt VAT 14%, but cafés/restaurants have different effective rates).
- **EGP currency:** locking as default, exposing as per-tenant Setting for future GCC expansion.
- **Hosting:** Azure UAE North unless you tell me data must stay in Egypt.
- **Billing:** Paymob for end-customer payments; manual invoicing for the first 5–10 tenants; Stripe later if international.
- **Offline-first POS:** in v1. Non-negotiable for Egypt WiFi reality.
- **Design typo:** "New proudct" → "New product" — minor, ping A.
- **"Premium" overload:** the word means both (a) plan tier ("ENTERPRISE" on login) and (b) discount tier (Premium Client). Suggest renaming the discount tier to "VIP Client" or "Discount Tier" to avoid confusion. Ping A.

---

## 11. Next concrete steps (unblocked — Phase A foundation)

Code path, in order. Each is a separate branch + PR for clean review.

1. **First: merge the security/auth-hardening branch into main** (already pushed, awaiting your approval). Doing the SaaS migration on top of the unhardened code would be a step backward.
2. **`refactor/rename-to-nizam`** — rename `BakeryPOS.API` → `Nizam.Api`, `BakeryPOS.API.Tests` → `Nizam.Api.Tests`, namespaces, csproj, sln. One mechanical commit. Solution still single-tenant after this — just rebranded.
3. **`feat/multi-tenant-foundation`** — add `Tenant` + `Branch` entities; backfill `TenantId` (and `BranchId` where applicable) onto existing tenant-scoped tables; one EF migration. Wire tenant resolution from JWT `tenant_id` claim → EF Core global query filter for every tenant-scoped DbSet. Add SQL Server Row-Level Security policies as belt-and-braces. End-to-end test: two tenants, each sees only their own data.
4. **`feat/order-envelope`** — introduce `Order` as the parent of `Sale` (Sale becomes a payment record on the Order). All B-aware shape from §2.1: `status` (always `closed` in A), `channel` (always `takeaway`), `tableId` (always `null`), `OrderItem.modifiers` (always `[]`), `OrderItem.status` / `firedAt` / `servedAt` (null in A). Existing sale flow continues to work via Order envelope.
5. **`feat/shift-management`** — add `Shift` entity, "Open POS Shift" PIN auth path, opening/closing cash float, variance calculation, Z-report PDF endpoint.
6. **`feat/parked-carts`** — add `ParkedCart` entity + endpoints. Frontend will hook this up.
7. **`feat/branch-scoped-roles`** — add `UserBranchRole`, refactor `[HasPermission]` filter to be branch-aware, refactor JWT to include branch claims.
8. **`feat/settings-and-audit-log`** — `Setting` entity (tax, currency, business name, receipt template), `AuditLog` entity, audit middleware on price changes / refunds / removals / cash variance.
9. **`infra/azure-blob-storage`** — move `wwwroot/images` writes to Azure Blob with tenant-prefixed keys.
10. **`infra/hangfire`** — replace `IHostedService` jobs with Hangfire on the same SQL DB.
11. **`refactor/i18n`** — drop hardcoded `fr-MA` in Program.cs; tenant-resolved culture (default `ar-EG`, fallback `en`).

Frontend path (loay, in parallel with steps 2–11):

12. Stand up React monorepo: `apps/portal`, `apps/pos`, `packages/design-system`, `packages/api-client`. Pull design tokens from Figma library.
13. Build auth + tenant/branch context provider.
14. Build RTL theming (logical-properties CSS, `dir="rtl"` toggle).
15. Implement designed screens against API one-by-one (Login → Branch Select → Opening Cash Float → Cashier → Dashboard → Products → Staff → Premium → Expenses).
16. Offline-first POS: IndexedDB queue, optimistic UI, idempotent sync keyed by client UUIDs.
17. Thermal printer integration (USB via Tauri).

Designer path (A, in parallel):

18. Close design gaps from §2.3 (close-of-shift, branches admin, branch-scoped role assignment, settings, Z-report, sidebar branch indicator, customer attach + discount apply on cashier, refund/void, generic copy).
19. Once §2.3 is done, start designing Phase B in parallel: floor plan editor, table picker, modifier picker, KDS layout, modifier admin (so Phase B isn't design-blocked the day Phase A ships).

Steps 1–4 are the irreversible architectural commitments. 5–11 are additive and reversible. The order above prioritises *getting tenancy and Order envelope right first* — those two land or the whole product is broken.

**Suggest:** start with step 1 (your call to merge the security branch), then step 2 (rename), since both are mechanical and unlock everything else.

---

## 12. Reference snapshots

Inspected in this round (all under `docs/planning/figma-snapshots/` for the ones I committed; full export at `F:/Work/BakeryPOS/Reborn/`):
- `board-overview.png` — FigJam strategic notes, kanban, founder differentiation thinking
- `login.png` — NIZAM brand, "Open POS Shift", EN/AR toggle, ENTERPRISE plan label
- `cashier-pos.png` — counter-service POS, parked carts, bakery categories, current order rail

Inspected from the JPG export (not committed — large set):
- Sidebar (definitive nav: Cashier / Dashboard / Premium / Products / Staff + Expense + Log out)
- Branch Select EN + AR (RTL parity confirmed)
- Opening Cash Float (numpad, EGP, "Open Session", verification banner)
- Dashboard (3 KPIs, Sales line, Expense vs Revenue bars, Top Products, Cashier Performance)
- Cashier-1 (BEST SELLER, SOLD OUT, piece-count, Tax 5%, Cash + Credit, Carts badge)
- Products (Stock / Photo / Name / Price / Status / Action — no SKU, no variants, no modifiers)
- User Management / Staff (Admin + Cashier roles only — no Branches column yet)
- Premium Client (% discount per customer — flat tier, not points)
- Expenses (3 KPIs + table + colored category chips)
- Main (overview of nav active states — confirms 5 modules)
