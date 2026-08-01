using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Messaging.Migrations
{
    /// Adds the shared consumer-side idempotency markers. This is an additive table;
    /// rolling back removes only processed-message markers and does not alter Outbox rows.
    public partial class AddInboxTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Inbox",
                schema: "messaging",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    HandlerType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inbox", x => new { x.MessageId, x.HandlerType });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Inbox",
                schema: "messaging");
        }
    }
}
