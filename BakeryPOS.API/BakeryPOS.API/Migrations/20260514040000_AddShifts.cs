using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <summary>
    /// Adds the Shifts table — cashier sessions (open/close with cash float + variance).
    /// Hand-written for the same Designer-snapshot reasons documented in earlier migrations.
    /// </summary>
    public partial class AddShifts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OpenedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<System.DateTime>(type: "datetime2", nullable: true),
                    ClosingCount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Variance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VarianceNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey("FK_Shifts_Users_UserId", x => x.UserId,
                        "Users", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_Shifts_TenantId_BranchId_UserId_ClosedAt", "Shifts",
                new[] { "TenantId", "BranchId", "UserId", "ClosedAt" });
            migrationBuilder.CreateIndex("IX_Shifts_UserId", "Shifts", "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Shifts");
        }
    }
}
