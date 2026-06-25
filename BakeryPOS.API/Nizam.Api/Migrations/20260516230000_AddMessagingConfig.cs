using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nizam.Api.Migrations
{
    /// <summary>
    /// Per-tenant messaging config (Phase 3.6 / messaging_notifications feature).
    ///   - MessagingConfigs: one row per tenant. Channel (None/Telegram/WhatsApp) + the
    ///     channel's credentials. Replaces the single global Telegram bot from appsettings.
    /// </summary>
    public partial class AddMessagingConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessagingConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TelegramBotToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TelegramChatId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WhatsAppPhoneNumberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WhatsAppAccessToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatsAppRecipient = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => { table.PrimaryKey("PK_MessagingConfigs", x => x.Id); });
            migrationBuilder.CreateIndex("IX_MessagingConfigs_TenantId", "MessagingConfigs", "TenantId", unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("MessagingConfigs");
        }
    }
}
