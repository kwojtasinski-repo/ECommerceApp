using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Inventory.Availability.Migrations
{
    /// <summary>
    /// Creates the <c>inventory</c> schema and all initial Inventory/Availability BC tables:
    /// <c>inventory.StockItems</c>, <c>inventory.Reservations</c>, <c>inventory.ProductSnapshots</c>,
    /// <c>inventory.PendingStockAdjustments</c>.
    /// Data impact: new schema and tables only — no existing data is modified.
    /// Rollback: <c>Down()</c> drops all four tables; the schema is left empty.
    /// Note: <c>inventory.StockItems</c> must be populated from legacy <c>Items.Quantity</c>
    /// and <c>inventory.ProductSnapshots</c> seeded from existing product data before the
    /// legacy <c>Item.Quantity</c> column can be removed (see ADR-0011 data migration plan).
    /// </summary>
    public partial class InitInventorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "PendingStockAdjustments",
                schema: "inventory",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    NewQuantity = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingStockAdjustments", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "ProductSnapshots",
                schema: "inventory",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDigital = table.Column<bool>(type: "bit", nullable: false),
                    CatalogStatus = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSnapshots", x => x.ProductId);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ReservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false, defaultValueSql: "0"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_OrderId_ProductId",
                schema: "inventory",
                table: "Reservations",
                columns: new[] { "OrderId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ProductId",
                schema: "inventory",
                table: "StockItems",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingStockAdjustments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "ProductSnapshots",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "StockItems",
                schema: "inventory");
        }
    }
}
