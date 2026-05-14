using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Adds the Order + OrderItem tables and a nullable Sale.OrderId FK. Hand-written —
    /// EF auto-gen produced incorrect output due to ongoing Designer-snapshot staleness
    /// (see RenamePaymentTypeCreditToTab / AddCustomerIsActive commit notes).
    /// </summary>
    public partial class AddOrderEnvelope : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Orders table
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: true),
                    CashierUserId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    FiredAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    ServedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey("FK_Orders_Users_CashierUserId", x => x.CashierUserId,
                        "Users", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_Orders_Customers_CustomerId", x => x.CustomerId,
                        "Customers", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_Orders_TenantId_BranchId_OpenedAt", "Orders",
                new[] { "TenantId", "BranchId", "OpenedAt" });
            migrationBuilder.CreateIndex("IX_Orders_CashierUserId", "Orders", "CashierUserId");
            migrationBuilder.CreateIndex("IX_Orders_CustomerId", "Orders", "CustomerId");

            // OrderItems table
            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Modifiers = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "[]"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FiredAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    ServedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey("FK_OrderItems_Orders_OrderId", x => x.OrderId,
                        "Orders", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_OrderItems_Products_ProductId", x => x.ProductId,
                        "Products", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_OrderItems_TenantId_OrderId", "OrderItems",
                new[] { "TenantId", "OrderId" });
            migrationBuilder.CreateIndex("IX_OrderItems_ProductId", "OrderItems", "ProductId");

            // Sale.OrderId FK (nullable while legacy SaleDetails + new Order/OrderItem coexist).
            migrationBuilder.AddColumn<int>(
                name: "OrderId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex("IX_Sales_OrderId", "Sales", "OrderId");
            migrationBuilder.AddForeignKey("FK_Sales_Orders_OrderId", "Sales", "OrderId",
                "Orders", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey("FK_Sales_Orders_OrderId", "Sales");
            migrationBuilder.DropIndex("IX_Sales_OrderId", "Sales");
            migrationBuilder.DropColumn("OrderId", "Sales");

            migrationBuilder.DropTable("OrderItems");
            migrationBuilder.DropTable("Orders");
        }
    }
}
