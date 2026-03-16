using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Inventory.Availability.Migrations
{
    /// <inheritdoc />
    public partial class RenameReservationsToStockHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_OrderId_ProductId",
                schema: "inventory",
                table: "Reservations");

            migrationBuilder.RenameTable(
                name: "Reservations",
                schema: "inventory",
                newName: "StockHolds",
                newSchema: "inventory");

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_OrderId_ProductId",
                schema: "inventory",
                table: "StockHolds",
                columns: new[] { "OrderId", "ProductId" },
                filter: "[Status] IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds",
                column: "ReservedAt",
                descending: new bool[0],
                filter: "[Status] IN (0, 1)")
                .Annotation("SqlServer:Include", new[] { "ProductId", "OrderId", "Quantity", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockHolds_ReservedAt",
                schema: "inventory",
                table: "StockHolds");

            migrationBuilder.DropIndex(
                name: "IX_StockHolds_OrderId_ProductId",
                schema: "inventory",
                table: "StockHolds");

            migrationBuilder.RenameTable(
                name: "StockHolds",
                schema: "inventory",
                newName: "Reservations",
                newSchema: "inventory");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrderId_ProductId",
                schema: "inventory",
                table: "Reservations",
                columns: new[] { "OrderId", "ProductId" });
        }
    }
}
