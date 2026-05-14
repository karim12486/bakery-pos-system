using BakeryPOS.API.Common.Tenancy;
using BakeryPOS.API.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace BakeryPOS.API.Data
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
                e.Property(x => x.Plan).HasMaxLength(40).IsRequired();
                e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
                e.Property(x => x.Locale).HasMaxLength(20).IsRequired();
                e.Property(x => x.Status).HasMaxLength(40).IsRequired();
                e.HasIndex(x => x.Slug).IsUnique();
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
            // Tenants table itself is NOT filtered — it's the source of truth for tenant lookup
            // (login flow, admin operations).
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
