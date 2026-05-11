using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BakeryPOS.API.Migrations
{
    /// <summary>
    /// Data-only migration: existing Sale rows that have <c>PaymentMethod = 'Credit'</c> are
    /// updated to <c>'Tab'</c> to match the renamed <see cref="Core.Enums.PaymentType.Tab"/>
    /// enum value. There is NO schema change — the column is still <c>nvarchar</c>.
    ///
    /// Background: the cashier UI's "Credit" button colloquially means card payment
    /// (Egyptian usage). The enum value <c>Credit</c> meant something else entirely (customer
    /// tab / store credit), which was a constant source of confusion in code. Renaming the
    /// enum member to <c>Tab</c> makes the intent obvious.
    /// </summary>
    public partial class RenamePaymentTypeCreditToTab : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Sales] SET [PaymentMethod] = 'Tab' WHERE [PaymentMethod] = 'Credit';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [Sales] SET [PaymentMethod] = 'Credit' WHERE [PaymentMethod] = 'Tab';");
        }
    }
}
