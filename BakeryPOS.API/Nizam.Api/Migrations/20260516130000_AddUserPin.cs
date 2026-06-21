using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Adds <c>Users.PinHash</c> — BCrypt hash of a 4–6 digit PIN for quick login on shared
    /// POS devices and for manager-override prompts. Nullable: most users have no PIN.
    /// </summary>
    public partial class AddUserPin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinHash", table: "Users", type: "nvarchar(max)", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("PinHash", "Users");
        }
    }
}
