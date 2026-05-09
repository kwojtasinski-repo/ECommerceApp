using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerceApp.Infrastructure.Sales.Coupons.Migrations
{
    /// <summary>
    /// Creates the Sales/Coupons BC schema (Slice 1).
    /// New tables: sales.Coupons, sales.CouponUsed.
    /// No data migration — parallel change; legacy dbo.Coupons / dbo.CouponUsed tables remain untouched until atomic switch (step 10).
    /// Rollback: drop sales.CouponUsed, then drop sales.Coupons (no data to preserve at this stage).
    /// </summary>
    public partial class InitCouponsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sales");

            migrationBuilder.CreateTable(
                name: "Coupons",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CouponUsed",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CouponId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponUsed", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                schema: "sales",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CouponUsed_CouponId",
                schema: "sales",
                table: "CouponUsed",
                column: "CouponId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CouponUsed_OrderId",
                schema: "sales",
                table: "CouponUsed",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coupons",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "CouponUsed",
                schema: "sales");
        }
    }
}
