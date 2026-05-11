using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <summary>
    /// Adds the <c>IsActive</c> column to <c>Customers</c> for soft-delete (audit finding H3).
    /// Existing rows default to <c>1</c> (active) so historical customers aren't accidentally hidden.
    /// Hand-written — EF's auto-gen produced an empty body due to a stale Designer in the
    /// preceding RenamePaymentTypeCreditToTab migration (see commit log).
    /// </summary>
    public partial class AddCustomerIsActive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Customers");
        }
    }
}
