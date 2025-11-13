using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <inheritdoc />
    public partial class FinalAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$GGde1VYWkAac5o.oD.KgcOInvxRYdl3sjfzIenTfgBarSqUYJaJke");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "$2a$11$j0.Uu8f2oXj0w/K0nQ9vQe2E.u2e/D9sU9vQ.f/gH3vR.p/gI5p/g");
        }
    }
}
