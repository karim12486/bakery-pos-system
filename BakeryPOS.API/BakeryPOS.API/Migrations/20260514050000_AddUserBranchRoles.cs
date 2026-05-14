using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <summary>
    /// UserBranchRoles — per-(user, branch) permission grants, unioned with the user's
    /// tenant-level <see cref="Core.Entities.User.Permissions"/> at authorization time.
    /// </summary>
    public partial class AddUserBranchRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserBranchRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Permissions = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBranchRoles", x => x.Id);
                    table.ForeignKey("FK_UserBranchRoles_Users_UserId", x => x.UserId,
                        "Users", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_UserBranchRoles_Branches_BranchId", x => x.BranchId,
                        "Branches", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_UserBranchRoles_TenantId_UserId_BranchId", "UserBranchRoles",
                new[] { "TenantId", "UserId", "BranchId" }, unique: true);
            migrationBuilder.CreateIndex("IX_UserBranchRoles_BranchId", "UserBranchRoles", "BranchId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("UserBranchRoles");
        }
    }
}
