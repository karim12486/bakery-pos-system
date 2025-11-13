using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminSeedFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "FullName", "IsActive", "PasswordHash", "Permissions", "Username" },
                values: new object[] { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Default Admin", true, "$2a$11$AQSw.SVZLTVfKmb5sHhK8ujItwL5JvS/DCiE6IzkwfwHkwAMAfbcm", -1, "admin" });
        }
    }
}
