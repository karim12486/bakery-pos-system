using Nizam.Api.Common.Tenancy;
using Nizam.Api.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Nizam.Api.Data
{
    public class AppDbContext : DbContext
    {
        private readonly ICurrentTenant _currentTenant;

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant) : base(options)
        {
            _currentTenant = currentTenant;
        }

        // Tenancy primitives
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Branch> Branches { get; set; }

        // Subscription plan catalog (tenant-AGNOSTIC — shared product catalog).
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanFeature> PlanFeatures { get; set; }
        public DbSet<PlanLimit> PlanLimits { get; set; }

        /// <summary>Per-tenant feature overrides (add-on grants / clawbacks). Managed by the
        /// super-admin / billing paths; consumed by PlanService when building the feature set.</summary>
        public DbSet<TenantFeatureOverride> TenantFeatureOverrides { get; set; }

        /// <summary>Platform operators (NIZAM team). Not tenant-scoped.</summary>
        public DbSet<SuperAdmin> SuperAdmins { get; set; }

        // Existing entities (all now have TenantId; the four operational ones also have BranchId).
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerPayment> CustomerPayments { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
        public DbSet<RemovalRequest> RemovalRequests { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<UserBranchRole> UserBranchRoles { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        // Phase B: modifier groups (controllers gated by [RequiresFeature("modifiers")]).
        public DbSet<ModifierGroup> ModifierGroups { get; set; }
        public DbSet<Modifier> Modifiers { get; set; }
        public DbSet<ProductModifierGroup> ProductModifierGroups { get; set; }

        /// <summary>Per-order-item snapshots of selected modifiers. Replaces the legacy
        /// <c>OrderItem.Modifiers</c> JSON column as the source of truth.</summary>
        public DbSet<OrderItemModifier> OrderItemModifiers { get; set; }

        // Phase B: floor plan (controllers gated by [RequiresFeature("tables")]).
        public DbSet<Area> Areas { get; set; }
        public DbSet<Table> Tables { get; set; }

        /// <summary>Per-occupancy records — open session = table occupied. Source of truth
        /// for <see cref="Table.Status"/>.</summary>
        public DbSet<TableSession> TableSessions { get; set; }

        /// <summary>Kitchen station types (Hot Kitchen, Bar, ...). Categories route to them;
        /// KDS screens display per (branch, station).</summary>
        public DbSet<KitchenStation> KitchenStations { get; set; }

        // Phase B: split-bill checks (controllers gated by [RequiresFeature("split_check")]).
        public DbSet<Check> Checks { get; set; }
        public DbSet<CheckItem> CheckItems { get; set; }

        // Phase B: table reservations (controllers gated by [RequiresFeature("tables")]).
        public DbSet<Reservation> Reservations { get; set; }

        // Phase 3: promotions (controllers gated by [RequiresFeature("promotions")]).
        public DbSet<Promotion> Promotions { get; set; }

        // Phase 3: loyalty (controllers gated by [RequiresFeature("loyalty")]).
        public DbSet<LoyaltyProgram> LoyaltyPrograms { get; set; }
        public DbSet<LoyaltyAccount> LoyaltyAccounts { get; set; }
        public DbSet<LoyaltyTransaction> LoyaltyTransactions { get; set; }

        // Phase 3.6: per-tenant messaging config (gated by [RequiresFeature("messaging_notifications")]).
        public DbSet<MessagingConfig> MessagingConfigs { get; set; }

        // Phase 3.7: inventory ops (gated by [RequiresFeature("inventory_ops")]).
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<StockTransfer> StockTransfers { get; set; }
        public DbSet<StockTransferItem> StockTransferItems { get; set; }
        public DbSet<WasteLogEntry> WasteLogEntries { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enums stored as strings for readability in the DB.
            modelBuilder.Entity<StockMovement>().Property(s => s.Type).HasConversion<string>();
            modelBuilder.Entity<Report>().Property(r => r.Type).HasConversion<string>();
            modelBuilder.Entity<Sale>().Property(s => s.PaymentMethod).HasConversion<string>();
            modelBuilder.Entity<RemovalRequest>().Property(r => r.Status).HasConversion<string>();
            modelBuilder.Entity<Order>().Property(o => o.Status).HasConversion<string>();
            modelBuilder.Entity<Order>().Property(o => o.Channel).HasConversion<string>();
            modelBuilder.Entity<OrderItem>().Property(i => i.Status).HasConversion<string>();

            // ---- Tenant + Branch shape ----
            modelBuilder.Entity<Tenant>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Slug).HasMaxLength(60).IsRequired();
                e.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
                e.Property(x => x.BillingCycle).HasMaxLength(20).IsRequired();
                e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                e.Property(x => x.Locale).HasMaxLength(20).IsRequired();
                e.Property(x => x.Status).HasMaxLength(40).IsRequired();
                e.HasIndex(x => x.Slug).IsUnique();
                e.HasIndex(x => x.PlanCode);
                // FK Tenant.PlanCode → Plan.Code. Restrict — never delete a plan that has tenants on it.
                e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanCode)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ---- Plan catalog (tenant-AGNOSTIC; no query filter, no TenantId column) ----
            modelBuilder.Entity<Plan>(e =>
            {
                e.HasKey(x => x.Code);
                e.Property(x => x.Code).HasMaxLength(40);
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Description).HasMaxLength(500);
                e.Property(x => x.MonthlyPriceEgp).HasColumnType("decimal(10,2)");
                e.Property(x => x.AnnualPriceEgp).HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<PlanFeature>(e =>
            {
                e.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
                e.Property(x => x.FeatureKey).HasMaxLength(60).IsRequired();
                e.HasIndex(x => new { x.PlanCode, x.FeatureKey }).IsUnique();
                e.HasOne(x => x.Plan).WithMany(p => p.Features).HasForeignKey(x => x.PlanCode)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PlanLimit>(e =>
            {
                e.Property(x => x.PlanCode).HasMaxLength(40).IsRequired();
                e.Property(x => x.LimitKey).HasMaxLength(60).IsRequired();
                e.HasIndex(x => new { x.PlanCode, x.LimitKey }).IsUnique();
                e.HasOne(x => x.Plan).WithMany(p => p.Limits).HasForeignKey(x => x.PlanCode)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Branch>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Address).HasMaxLength(300);
                e.Property(x => x.Timezone).HasMaxLength(60).IsRequired();
                e.Property(x => x.Currency).HasMaxLength(3);
                e.Property(x => x.TaxRate).HasColumnType("decimal(5,4)");
                e.HasOne(x => x.Tenant).WithMany(t => t.Branches).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.Name });
            });

            // ---- TenantId index on every tenant-scoped entity (filter-fast lookups) ----
            modelBuilder.Entity<User>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<Product>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<Category>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<Customer>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<CustomerPayment>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<Sale>().HasIndex(x => new { x.TenantId, x.BranchId });
            modelBuilder.Entity<SaleDetail>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<StockMovement>().HasIndex(x => new { x.TenantId, x.BranchId });
            modelBuilder.Entity<Expense>().HasIndex(x => new { x.TenantId, x.BranchId });
            modelBuilder.Entity<ExpenseCategory>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<Report>().HasIndex(x => x.TenantId);
            modelBuilder.Entity<RemovalRequest>().HasIndex(x => new { x.TenantId, x.BranchId });
            modelBuilder.Entity<Order>().HasIndex(x => new { x.TenantId, x.BranchId, x.OpenedAt });
            modelBuilder.Entity<OrderItem>().HasIndex(x => new { x.TenantId, x.OrderId });
            modelBuilder.Entity<Shift>().HasIndex(x => new { x.TenantId, x.BranchId, x.UserId, x.ClosedAt });
            modelBuilder.Entity<UserBranchRole>()
                .HasIndex(x => new { x.TenantId, x.UserId, x.BranchId }).IsUnique();

            modelBuilder.Entity<Setting>(e =>
            {
                e.Property(x => x.Key).HasMaxLength(120).IsRequired();
                e.Property(x => x.Value).HasMaxLength(4000).IsRequired();
                e.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
            });

            modelBuilder.Entity<AuditLog>(e =>
            {
                e.Property(x => x.Username).HasMaxLength(50);
                e.Property(x => x.Action).HasMaxLength(80).IsRequired();
                e.Property(x => x.EntityType).HasMaxLength(60);
                e.Property(x => x.Details).HasColumnType("nvarchar(max)");
                e.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max
                e.HasIndex(x => new { x.TenantId, x.At });
                e.HasIndex(x => new { x.TenantId, x.Action });
                e.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            });
            modelBuilder.Entity<UserBranchRole>(e =>
            {
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Shift shape
            modelBuilder.Entity<Shift>(e =>
            {
                e.Property(x => x.VarianceNotes).HasMaxLength(500);
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Order shape
            modelBuilder.Entity<Order>(e =>
            {
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Channel).HasMaxLength(20).IsRequired();
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<OrderItem>(e =>
            {
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Notes).HasMaxLength(300);
                e.HasOne(x => x.Order).WithMany(o => o.Items).HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            // Sale → Order is the 1:1 envelope. Optional FK while SaleDetail-legacy and the new
            // Order model coexist; new sales always populate it.
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Order).WithMany().HasForeignKey(s => s.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Idempotency unique key is per-tenant (so different tenants can use overlapping keys).
            modelBuilder.Entity<IdempotencyRecord>(e =>
            {
                e.Property(x => x.Key).HasMaxLength(80).IsRequired();
                e.Property(x => x.Endpoint).HasMaxLength(120).IsRequired();
                e.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.TenantId, x.Endpoint, x.Key }).IsUnique();
            });

            // ---- Modifier groups (Phase B) ----
            modelBuilder.Entity<ModifierGroup>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Description).HasMaxLength(300);
                e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.SortOrder });
            });
            modelBuilder.Entity<Modifier>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.PriceDelta).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.Group).WithMany(g => g.Modifiers).HasForeignKey(x => x.ModifierGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.ModifierGroupId, x.SortOrder });
            });
            modelBuilder.Entity<ProductModifierGroup>(e =>
            {
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.ModifierGroup).WithMany(g => g.ProductLinks).HasForeignKey(x => x.ModifierGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.ProductId, x.ModifierGroupId }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.ProductId, x.SortOrder });
            });
            modelBuilder.Entity<OrderItemModifier>(e =>
            {
                e.Property(x => x.GroupName).HasMaxLength(120).IsRequired();
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.PriceDelta).HasColumnType("decimal(18,2)");
                // Cascade with the parent order item — deleting the item deletes its snapshots.
                e.HasOne(x => x.OrderItem).WithMany(o => o.AppliedModifiers).HasForeignKey(x => x.OrderItemId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.OrderItemId });
            });

            // ---- Floor plan (Phase B) ----
            modelBuilder.Entity<Area>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.SortOrder });
            });
            modelBuilder.Entity<Table>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(40).IsRequired();
                e.Property(x => x.Shape).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.Status).HasMaxLength(20).HasConversion<string>();
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Area).WithMany(a => a.Tables).HasForeignKey(x => x.AreaId)
                    // Restrict (not cascade) so deleting an Area with active tables surfaces
                    // a real error instead of silently disappearing seating.
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.Name }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.AreaId });
            });
            modelBuilder.Entity<TableSession>(e =>
            {
                e.Property(x => x.Notes).HasMaxLength(300);
                e.Property(x => x.RowVersion).IsRowVersion();
                e.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.ServerUser).WithMany().HasForeignKey(x => x.ServerUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.SetNull);
                // Fast lookup of the open session for a table (filtered by ClosedAt in queries).
                e.HasIndex(x => new { x.TenantId, x.TableId, x.ClosedAt });
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.ClosedAt });
            });
            modelBuilder.Entity<KitchenStation>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.SortOrder });
            });
            modelBuilder.Entity<Check>(e =>
            {
                e.Property(x => x.Label).HasMaxLength(60).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.PaymentMethod).HasMaxLength(20).HasConversion<string>();
                e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.OrderId });
            });
            modelBuilder.Entity<CheckItem>(e =>
            {
                e.HasOne(x => x.Check).WithMany(c => c.Items).HasForeignKey(x => x.CheckId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId)
                    .OnDelete(DeleteBehavior.Restrict);
                // An order item belongs to at most one check.
                e.HasIndex(x => new { x.TenantId, x.OrderItemId }).IsUnique();
                e.HasIndex(x => new { x.TenantId, x.CheckId });
            });
            modelBuilder.Entity<TenantFeatureOverride>(e =>
            {
                e.Property(x => x.FeatureKey).HasMaxLength(60).IsRequired();
                e.Property(x => x.Reason).HasMaxLength(200);
                // One override row per (tenant, feature).
                e.HasIndex(x => new { x.TenantId, x.FeatureKey }).IsUnique();
            });
            modelBuilder.Entity<SuperAdmin>(e =>
            {
                e.Property(x => x.Username).HasMaxLength(80).IsRequired();
                e.Property(x => x.FullName).HasMaxLength(120);
                e.HasIndex(x => x.Username).IsUnique();
            });
            modelBuilder.Entity<Promotion>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(120).IsRequired();
                e.Property(x => x.Code).HasMaxLength(40);
                e.Property(x => x.Type).HasMaxLength(20).HasConversion<string>();
                // Coupon code unique per tenant (filtered to non-null in queries by lookup).
                e.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            });
            modelBuilder.Entity<LoyaltyProgram>(e =>
            {
                // One program per tenant.
                e.HasIndex(x => x.TenantId).IsUnique();
            });
            modelBuilder.Entity<LoyaltyAccount>(e =>
            {
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
                // One wallet per (tenant, customer).
                e.HasIndex(x => new { x.TenantId, x.CustomerId }).IsUnique();
            });
            modelBuilder.Entity<LoyaltyTransaction>(e =>
            {
                e.Property(x => x.Type).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.Reason).HasMaxLength(200);
                e.HasOne(x => x.Account).WithMany(a => a.Transactions).HasForeignKey(x => x.LoyaltyAccountId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.LoyaltyAccountId, x.CreatedAt });
            });
            modelBuilder.Entity<MessagingConfig>(e =>
            {
                e.Property(x => x.Channel).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.TelegramBotToken).HasMaxLength(200);
                e.Property(x => x.TelegramChatId).HasMaxLength(100);
                e.Property(x => x.WhatsAppPhoneNumberId).HasMaxLength(100);
                e.Property(x => x.WhatsAppAccessToken).HasMaxLength(500);
                e.Property(x => x.WhatsAppRecipient).HasMaxLength(50);
                // One config row per tenant.
                e.HasIndex(x => x.TenantId).IsUnique();
            });
            modelBuilder.Entity<Supplier>(e =>
            {
                e.Property(x => x.Name).HasMaxLength(160).IsRequired();
                e.Property(x => x.ContactName).HasMaxLength(120);
                e.Property(x => x.Phone).HasMaxLength(30);
                e.Property(x => x.Email).HasMaxLength(160);
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasIndex(x => new { x.TenantId, x.Name });
            });
            modelBuilder.Entity<PurchaseOrder>(e =>
            {
                e.Property(x => x.Status).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.Reference).HasMaxLength(80);
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasMany(x => x.Items).WithOne(i => i.PurchaseOrder).HasForeignKey(i => i.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.Status });
            });
            modelBuilder.Entity<PurchaseOrderItem>(e =>
            {
                e.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });
            });
            modelBuilder.Entity<StockTransfer>(e =>
            {
                e.Property(x => x.Status).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.Reference).HasMaxLength(80);
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasMany(x => x.Items).WithOne(i => i.StockTransfer).HasForeignKey(i => i.StockTransferId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantId, x.Status });
            });
            modelBuilder.Entity<StockTransferItem>(e =>
            {
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.TenantId, x.StockTransferId });
            });
            modelBuilder.Entity<WasteLogEntry>(e =>
            {
                e.Property(x => x.Reason).HasMaxLength(20).HasConversion<string>();
                e.Property(x => x.EstimatedCost).HasColumnType("decimal(18,2)");
                e.Property(x => x.Notes).HasMaxLength(500);
                e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            });
            modelBuilder.Entity<Reservation>(e =>
            {
                e.Property(x => x.CustomerName).HasMaxLength(120).IsRequired();
                e.Property(x => x.Phone).HasMaxLength(30);
                e.Property(x => x.Notes).HasMaxLength(300);
                e.Property(x => x.Status).HasMaxLength(20).HasConversion<string>();
                e.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(x => new { x.TenantId, x.BranchId, x.ReservedFor });
                e.HasIndex(x => new { x.TenantId, x.Status });
            });
            // Category → KitchenStation routing (nullable; SetNull so deleting a station
            // un-routes its categories rather than cascading them away).
            modelBuilder.Entity<Category>(e =>
            {
                e.HasOne(x => x.KitchenStation).WithMany().HasForeignKey(x => x.KitchenStationId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // ---- Global query filters: every query is automatically scoped by TenantId. ----
            // CLOSED filter: when _currentTenant.TenantId is null, the predicate `x.TenantId == null`
            // is never true (TenantId is non-nullable int), so the query returns ZERO rows.
            // This is the safe-by-default stance — a request that somehow loses its tenant context
            // (bug, stripped JWT claim, anonymous endpoint) sees no data instead of all data.
            //
            // Cross-tenant access (login lookup before tenant is known, seeder, background jobs
            // that iterate tenants) MUST use IgnoreQueryFilters() explicitly. That intent is
            // auditable in code review.
            modelBuilder.Entity<User>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Product>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Category>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Customer>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<CustomerPayment>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Sale>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<SaleDetail>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<StockMovement>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Expense>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Report>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<RemovalRequest>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Branch>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Order>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<OrderItem>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Shift>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<UserBranchRole>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Setting>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<AuditLog>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<ModifierGroup>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Modifier>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<ProductModifierGroup>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<OrderItemModifier>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Area>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Table>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<TableSession>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<KitchenStation>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Check>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<CheckItem>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Reservation>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Promotion>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<LoyaltyProgram>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<LoyaltyAccount>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<LoyaltyTransaction>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<MessagingConfig>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Supplier>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<PurchaseOrderItem>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<StockTransfer>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<StockTransferItem>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<WasteLogEntry>().HasQueryFilter(x => x.TenantId == _currentTenant.TenantId);
            // Tenants + Plans + PlanFeatures + PlanLimits are NOT filtered — they're tenant-agnostic
            // master data (login flow, plan lookup, admin operations).
        }

        /// <summary>
        /// Stamp TenantId on new entities before save. Catches the case where a service forgets
        /// to set it; the filter would later hide the row, but writing the wrong TenantId is
        /// worse than failing fast. Tenant must be in scope for any tenant-scoped write.
        /// </summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampTenantOnAdded();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            StampTenantOnAdded();
            return base.SaveChanges();
        }

        private void StampTenantOnAdded()
        {
            if (_currentTenant.TenantId == null) return; // anonymous / seeder paths stamp explicitly

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added) continue;
                var tenantIdProp = entry.Metadata.FindProperty("TenantId");
                if (tenantIdProp == null) continue;

                var current = entry.CurrentValues["TenantId"];
                if (current is int v && v == 0)
                {
                    entry.CurrentValues["TenantId"] = _currentTenant.TenantId.Value;
                }
            }
        }
    }
}
