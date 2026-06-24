using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Promotions (Phase 3.2 / promotions feature). Rule-driven order discounts — coupon code
    /// or auto-apply, with a minimum order, optional window, and optional redemption cap.
    /// </summary>
    public partial class AddPromotions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinOrderAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StartsAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    EndsAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: true),
                    RedemptionCount = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                });
            migrationBuilder.CreateIndex("IX_Promotions_TenantId_Code", "Promotions",
                new[] { "TenantId", "Code" }, unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Promotions");
        }
    }
}
