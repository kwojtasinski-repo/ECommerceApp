using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Sales.Payments.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTokenAndUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentId",
                schema: "payments",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "payments",
                table: "Payments",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentId",
                schema: "payments",
                table: "Payments",
                column: "PaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UserId",
                schema: "payments",
                table: "Payments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentId",
                schema: "payments",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_UserId",
                schema: "payments",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                schema: "payments",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "payments",
                table: "Payments");
        }
    }
}
