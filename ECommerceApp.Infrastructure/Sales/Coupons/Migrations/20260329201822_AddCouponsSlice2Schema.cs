using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Sales.Coupons.Migrations
{
    /// <summary>
    /// Coupons Slice 2 schema additions (ADR-0016 §9.11):
    /// - sales.Coupons: +RulesJson (nvarchar(max)), +Version (rowversion), +BypassOversizeGuard (bit, default false)
    /// - sales.CouponUsed: +RuntimeCouponSnapshot (nvarchar(max)), +UserId (nvarchar(max))
    /// - NEW tables: sales.CouponScopeTargets, sales.CouponApplicationRecords, sales.SpecialEvents
    /// Existing data is unaffected — new columns are nullable or have defaults.
    /// </summary>
    public partial class AddCouponsSlice2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuntimeCouponSnapshot",
                schema: "sales",
                table: "CouponUsed",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "sales",
                table: "CouponUsed",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BypassOversizeGuard",
                schema: "sales",
                table: "Coupons",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesJson",
                schema: "sales",
                table: "Coupons",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "sales",
                table: "Coupons",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CouponApplicationRecords",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CouponUsedId = table.Column<int>(type: "int", nullable: false),
                    CouponCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WasReversed = table.Column<bool>(type: "bit", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponApplicationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CouponScopeTargets",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CouponId = table.Column<int>(type: "int", nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    TargetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponScopeTargets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialEvents",
                schema: "sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CouponApplicationRecords_CouponUsedId",
                schema: "sales",
                table: "CouponApplicationRecords",
                column: "CouponUsedId");

            migrationBuilder.CreateIndex(
                name: "IX_CouponScopeTargets_CouponId",
                schema: "sales",
                table: "CouponScopeTargets",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialEvents_Code",
                schema: "sales",
                table: "SpecialEvents",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CouponApplicationRecords",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "CouponScopeTargets",
                schema: "sales");

            migrationBuilder.DropTable(
                name: "SpecialEvents",
                schema: "sales");

            migrationBuilder.DropColumn(
                name: "RuntimeCouponSnapshot",
                schema: "sales",
                table: "CouponUsed");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "sales",
                table: "CouponUsed");

            migrationBuilder.DropColumn(
                name: "BypassOversizeGuard",
                schema: "sales",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "RulesJson",
                schema: "sales",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "sales",
                table: "Coupons");
        }
    }
}
