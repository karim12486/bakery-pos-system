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


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Enums stored as strings for readability in the DB.
            modelBuilder.Entity<StockMovement>().Property(s => s.Type).HasConversion<string>();
            modelBuilder.Entity<Report>().Property(r => r.Type).HasConversion<string>();
            modelBuilder.Entity<Sale>().Property(s => s.PaymentMethod).HasConversion<string>();
            modelBuilder.Entity<RemovalRequest>().Property(r => r.Status).HasConversion<string>();

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

            // Idempotency unique key is per-tenant (so different tenants can use overlapping keys).
            modelBuilder.Entity<IdempotencyRecord>(e =>
            {
                e.Property(x => x.Key).HasMaxLength(80).IsRequired();
                e.Property(x => x.Endpoint).HasMaxLength(120).IsRequired();
                e.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.TenantId, x.Endpoint, x.Key }).IsUnique();
            });

            // ---- Global query filters: every query is automatically scoped by TenantId. ----
            // The lambda captures `this` (the AppDbContext) so `_currentTenant.TenantId` is read
            // at QUERY time, not at OnModelCreating time. That's how each request sees its own data.
            //
            // Anonymous / cross-tenant operations (signup, seeder, certain background jobs) must
            // use IgnoreQueryFilters() explicitly. That intent is auditable in code review.
            modelBuilder.Entity<User>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Product>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Category>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Customer>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<CustomerPayment>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Sale>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<SaleDetail>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<StockMovement>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Expense>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Report>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<RemovalRequest>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<Branch>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
            modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(x => _currentTenant.TenantId == null || x.TenantId == _currentTenant.TenantId);
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
