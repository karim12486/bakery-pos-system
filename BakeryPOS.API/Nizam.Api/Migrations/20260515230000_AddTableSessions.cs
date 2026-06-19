using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Table occupancy sessions (Phase B dine-in). One open session per occupied table; ties
    /// table → server → order → guest count. RowVersion guards against double-seating races.
    /// </summary>
    public partial class AddTableSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TableSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: false),
                    ServerUserId = table.Column<int>(type: "int", nullable: true),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    GuestCount = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TableSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TableSessions_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableSessions_Users_ServerUserId",
                        column: x => x.ServerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TableSessions_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull);
                });
            migrationBuilder.CreateIndex("IX_TableSessions_OrderId", "TableSessions", "OrderId");
            migrationBuilder.CreateIndex("IX_TableSessions_ServerUserId", "TableSessions", "ServerUserId");
            migrationBuilder.CreateIndex("IX_TableSessions_TenantId_TableId_ClosedAt",
                "TableSessions", new[] { "TenantId", "TableId", "ClosedAt" });
            migrationBuilder.CreateIndex("IX_TableSessions_TenantId_BranchId_ClosedAt",
                "TableSessions", new[] { "TenantId", "BranchId", "ClosedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("TableSessions");
        }
    }
}
