using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Inventory.Availability.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAuditTableAndIndexAmendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds");

            migrationBuilder.CreateTable(
                name: "StockAuditEntries",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ChangeType = table.Column<byte>(type: "tinyint", nullable: false),
                    QuantityBefore = table.Column<int>(type: "int", nullable: false),
                    QuantityAfter = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAuditEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds",
                column: "ReservedAt",
                descending: new bool[0],
                filter: "[Status] IN (0, 1)")
                .Annotation("SqlServer:Include", new[] { "ProductId", "OrderId", "Quantity", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAuditEntries_OccurredAt",
                schema: "inventory",
                table: "StockAuditEntries",
                column: "OccurredAt",
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "ProductId", "ChangeType", "QuantityBefore", "QuantityAfter" });

            migrationBuilder.CreateIndex(
                name: "IX_StockAuditEntries_ProductId_OccurredAt",
                schema: "inventory",
                table: "StockAuditEntries",
                columns: new[] { "ProductId", "OccurredAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAuditEntries",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds");

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds",
                column: "ReservedAt",
                descending: new bool[0],
                filter: "[Status] IN (0, 1)")
                .Annotation("SqlServer:Include", new[] { "ProductId", "OrderId", "Quantity", "ExpiresAt" });
        }
    }
}
