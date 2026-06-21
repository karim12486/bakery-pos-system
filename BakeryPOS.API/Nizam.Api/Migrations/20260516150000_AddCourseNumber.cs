using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Adds <c>OrderItems.CourseNumber</c> (default 1) for coursing — servers group items into
    /// courses and fire them in waves so the kitchen paces the meal.
    /// </summary>
    public partial class AddCourseNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseNumber", table: "OrderItems", type: "int", nullable: false, defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("CourseNumber", "OrderItems");
        }
    }
}
