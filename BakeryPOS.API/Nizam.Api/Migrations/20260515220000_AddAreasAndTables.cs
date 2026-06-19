using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Floor-plan foundation (Phase B / Pro-tier gated by [RequiresFeature("tables")]).
    ///   - Areas: branch-scoped logical sections (Indoor, Outdoor, Bar, ...).
    ///   - Tables: per-branch seats with x/y/width/height/shape for the editor.
    /// TableSession (occupancy + server assignment) ships in the next branch
    /// (feat/dine-in-channel).
    /// </summary>
    public partial class AddAreasAndTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("IX_Areas_TenantId_BranchId_Name", "Areas",
                new[] { "TenantId", "BranchId", "Name" }, unique: true);
            migrationBuilder.CreateIndex("IX_Areas_TenantId_BranchId_SortOrder", "Areas",
                new[] { "TenantId", "BranchId", "SortOrder" });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Shape = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    X = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Y = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Width = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Height = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Rotation = table.Column<decimal>(type: "decimal(6,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tables_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tables_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex("IX_Tables_TenantId_BranchId_Name", "Tables",
                new[] { "TenantId", "BranchId", "Name" }, unique: true);
            migrationBuilder.CreateIndex("IX_Tables_TenantId_BranchId_AreaId", "Tables",
                new[] { "TenantId", "BranchId", "AreaId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Tables");
            migrationBuilder.DropTable("Areas");
        }
    }
}
