using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Kitchen stations + routing (Phase B / KDS prereq).
    ///   - KitchenStations: tenant-scoped station types (Hot Kitchen, Bar, ...).
    ///   - Categories.KitchenStationId: routes a category's products to a station (nullable FK).
    ///   - OrderItems.KitchenStationId: snapshot of the route at order time (nullable, no FK —
    ///     denormalised so deleting a station doesn't break historical order items).
    /// </summary>
    public partial class AddKitchenStations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitchenStations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenStations", x => x.Id);
                });
            migrationBuilder.CreateIndex("IX_KitchenStations_TenantId_Name", "KitchenStations",
                new[] { "TenantId", "Name" }, unique: true);
            migrationBuilder.CreateIndex("IX_KitchenStations_TenantId_SortOrder", "KitchenStations",
                new[] { "TenantId", "SortOrder" });

            migrationBuilder.AddColumn<int>(
                name: "KitchenStationId", table: "Categories", type: "int", nullable: true);
            migrationBuilder.CreateIndex("IX_Categories_KitchenStationId", "Categories", "KitchenStationId");
            migrationBuilder.AddForeignKey(
                name: "FK_Categories_KitchenStations_KitchenStationId",
                table: "Categories",
                column: "KitchenStationId",
                principalTable: "KitchenStations",
                principalColumn: "Id",
                onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull);

            migrationBuilder.AddColumn<int>(
                name: "KitchenStationId", table: "OrderItems", type: "int", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("KitchenStationId", "OrderItems");
            migrationBuilder.DropForeignKey("FK_Categories_KitchenStations_KitchenStationId", "Categories");
            migrationBuilder.DropIndex("IX_Categories_KitchenStationId", "Categories");
            migrationBuilder.DropColumn("KitchenStationId", "Categories");
            migrationBuilder.DropTable("KitchenStations");
        }
    }
}
