using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Subscription plan catalog (tenant-agnostic master data) + tenant subscription metadata.
    /// Phase 2 foundation — every plan-gated feature down the line reads from these tables.
    ///
    /// Schema changes (all additive except the Plan→PlanCode column rename on Tenants):
    ///   - CREATE Plans (PK Code)
    ///   - CREATE PlanFeatures (FK → Plans)
    ///   - CREATE PlanLimits (FK → Plans)
    ///   - SEED starter/growth/pro rows (so the Tenants.PlanCode FK can be enforced)
    ///   - RENAME Tenants.Plan → Tenants.PlanCode
    ///   - ADD Tenants.BillingCycle, Tenants.TrialEndsAt
    ///   - BACKFILL existing tenants from legacy plan codes onto real plan codes
    ///   - ADD FK Tenants.PlanCode → Plans.Code (Restrict)
    /// </summary>
    public partial class AddSubscriptionPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Plans (master catalog)
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: ""),
                    MonthlyPriceEgp = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    AnnualPriceEgp = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsPubliclyVisible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Code);
                });

            // 2. PlanFeatures
            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    PlanCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_Plans_PlanCode",
                        column: x => x.PlanCode,
                        principalTable: "Plans",
                        principalColumn: "Code",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_PlanFeatures_PlanCode_FeatureKey", "PlanFeatures",
                new[] { "PlanCode", "FeatureKey" }, unique: true);

            // 3. PlanLimits
            migrationBuilder.CreateTable(
                name: "PlanLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    PlanCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LimitKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanLimits_Plans_PlanCode",
                        column: x => x.PlanCode,
                        principalTable: "Plans",
                        principalColumn: "Code",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_PlanLimits_PlanCode_LimitKey", "PlanLimits",
                new[] { "PlanCode", "LimitKey" }, unique: true);

            // 4. Seed the 3 plan rows so the Tenants.PlanCode FK can be enforced.
            //    Content is authoritative-at-migration-time; the DbInitializer keeps it in sync going forward.
            migrationBuilder.Sql(@"
INSERT INTO Plans (Code, Name, Description, MonthlyPriceEgp, AnnualPriceEgp, SortOrder, IsActive, IsPubliclyVisible, CreatedAt, UpdatedAt) VALUES
('starter', 'Starter', 'Single-branch POS for small operators.',     999.00,  9990.00, 10, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('growth',  'Growth',  'Multi-branch with modifiers and loyalty.',  1999.00, 19990.00, 20, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME()),
('pro',     'Pro',     'Restaurants, KDS, white-label, full ops.',   3499.00, 34990.00, 30, 1, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
");

            // 5. Tenant column changes
            migrationBuilder.RenameColumn(name: "Plan", table: "Tenants", newName: "PlanCode");
            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "monthly");
            migrationBuilder.AddColumn<System.DateTime>(
                name: "TrialEndsAt",
                table: "Tenants",
                type: "datetime2",
                nullable: true);

            // 6. Backfill legacy values: any tenant currently on "trial" or unknown → "growth" trialing.
            migrationBuilder.Sql(@"
UPDATE Tenants SET PlanCode = 'growth'  WHERE PlanCode = 'trial';
UPDATE Tenants SET PlanCode = 'starter' WHERE PlanCode NOT IN ('starter','growth','pro');
");

            // 7. Index + FK on Tenants.PlanCode
            migrationBuilder.CreateIndex("IX_Tenants_PlanCode", "Tenants", "PlanCode");
            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Plans_PlanCode",
                table: "Tenants",
                column: "PlanCode",
                principalTable: "Plans",
                principalColumn: "Code",
                onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_Tenants_Plans_PlanCode", "Tenants");
            migrationBuilder.DropIndex("IX_Tenants_PlanCode", "Tenants");
            migrationBuilder.DropColumn("TrialEndsAt", "Tenants");
            migrationBuilder.DropColumn("BillingCycle", "Tenants");
            migrationBuilder.RenameColumn(name: "PlanCode", table: "Tenants", newName: "Plan");

            migrationBuilder.DropTable("PlanLimits");
            migrationBuilder.DropTable("PlanFeatures");
            migrationBuilder.DropTable("Plans");
        }
    }
}
