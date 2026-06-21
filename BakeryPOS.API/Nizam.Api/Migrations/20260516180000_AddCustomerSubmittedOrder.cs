using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Adds <c>Orders.IsCustomerSubmitted</c> (default false) — flags orders whose items a guest
    /// added via the QR table menu, so staff review them before firing.
    /// </summary>
    public partial class AddCustomerSubmittedOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustomerSubmitted", table: "Orders", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("IsCustomerSubmitted", "Orders");
        }
    }
}
