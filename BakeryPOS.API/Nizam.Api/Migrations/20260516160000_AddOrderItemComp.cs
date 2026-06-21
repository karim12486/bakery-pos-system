using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Adds <c>OrderItems.IsComped</c> (default false) — a comped item is still made by the
    /// kitchen but not charged to the guest (manager goodwill). Setting it requires an override.
    /// </summary>
    public partial class AddOrderItemComp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComped", table: "OrderItems", type: "bit", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("IsComped", "OrderItems");
        }
    }
}
