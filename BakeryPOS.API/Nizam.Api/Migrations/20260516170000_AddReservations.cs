using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Table reservations (Phase B). Branch-scoped bookings with guest details, requested time,
    /// optional table assignment, status, and a reminder-sent timestamp for the Hangfire job.
    /// </summary>
    public partial class AddReservations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    ReservedFor = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    TableId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReminderSentAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reservations_Tables_TableId",
                        column: x => x.TableId,
                        principalTable: "Tables",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull);
                });
            migrationBuilder.CreateIndex("IX_Reservations_TableId", "Reservations", "TableId");
            migrationBuilder.CreateIndex("IX_Reservations_TenantId_BranchId_ReservedFor",
                "Reservations", new[] { "TenantId", "BranchId", "ReservedFor" });
            migrationBuilder.CreateIndex("IX_Reservations_TenantId_Status",
                "Reservations", new[] { "TenantId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Reservations");
        }
    }
}
