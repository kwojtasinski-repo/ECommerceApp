using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Presale.Checkout.Migrations
{
    /// <summary>
    /// Creates the <c>presale</c> schema and all initial Presale/Checkout BC tables:
    /// <c>presale.CartLines</c>, <c>presale.SoftReservations</c>, <c>presale.StockSnapshots</c>.
    /// Data impact: new schema and tables only — no existing data is modified.
    /// Rollback: <c>Down()</c> drops all three tables; the schema is left empty.
    /// Note: <c>presale.StockSnapshots</c> will be populated by <c>StockAvailabilityChanged</c>
    /// integration messages published by <c>StockService</c> after first inventory operations.
    /// No seed data is required — the tables start empty (see ADR-0012 Slice 1).
    /// </summary>
    public partial class InitPresaleSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "presale");

            migrationBuilder.CreateTable(
                name: "CartLines",
                schema: "presale",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartLines", x => new { x.UserId, x.ProductId });
                });

            migrationBuilder.CreateTable(
                name: "SoftReservations",
                schema: "presale",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftReservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockSnapshots",
                schema: "presale",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSnapshots", x => x.ProductId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoftReservations_ProductId_UserId",
                schema: "presale",
                table: "SoftReservations",
                columns: new[] { "ProductId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockSnapshots_ProductId",
                schema: "presale",
                table: "StockSnapshots",
                column: "ProductId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartLines",
                schema: "presale");

            migrationBuilder.DropTable(
                name: "SoftReservations",
                schema: "presale");

            migrationBuilder.DropTable(
                name: "StockSnapshots",
                schema: "presale");
        }
    }
}
