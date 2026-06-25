using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Loyalty points (Phase 3.3 / loyalty feature).
    ///   - LoyaltyPrograms: one per tenant (earn/redeem rates, minimum).
    ///   - LoyaltyAccounts: one wallet per (tenant, customer).
    ///   - LoyaltyTransactions: immutable earn/redeem/adjust ledger.
    /// </summary>
    public partial class AddLoyalty : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyPrograms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    EarnPointsPerCurrency = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RedeemCurrencyPerPoint = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinRedeemPoints = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_LoyaltyPrograms", x => x.Id); });
            migrationBuilder.CreateIndex("IX_LoyaltyPrograms_TenantId", "LoyaltyPrograms", "TenantId", unique: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PointsBalance = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyAccounts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_LoyaltyAccounts_CustomerId", "LoyaltyAccounts", "CustomerId");
            migrationBuilder.CreateIndex("IX_LoyaltyAccounts_TenantId_CustomerId", "LoyaltyAccounts",
                new[] { "TenantId", "CustomerId" }, unique: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    LoyaltyAccountId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RelatedSaleId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_LoyaltyAccounts_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalTable: "LoyaltyAccounts",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_LoyaltyTransactions_LoyaltyAccountId", "LoyaltyTransactions", "LoyaltyAccountId");
            migrationBuilder.CreateIndex("IX_LoyaltyTransactions_TenantId_LoyaltyAccountId_CreatedAt",
                "LoyaltyTransactions", new[] { "TenantId", "LoyaltyAccountId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("LoyaltyTransactions");
            migrationBuilder.DropTable("LoyaltyAccounts");
            migrationBuilder.DropTable("LoyaltyPrograms");
        }
    }
}
