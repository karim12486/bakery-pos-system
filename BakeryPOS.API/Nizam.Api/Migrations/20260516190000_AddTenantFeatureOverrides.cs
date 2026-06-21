using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Per-tenant feature overrides (Phase 3) — the add-on grant / sales escape-hatch mechanism
    /// that layers on top of a tenant's plan. One row per (tenant, feature), unique.
    /// </summary>
    public partial class AddTenantFeatureOverrides : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantFeatureOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantFeatureOverrides", x => x.Id);
                });
            migrationBuilder.CreateIndex("IX_TenantFeatureOverrides_TenantId_FeatureKey",
                "TenantFeatureOverrides", new[] { "TenantId", "FeatureKey" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("TenantFeatureOverrides");
        }
    }
}
