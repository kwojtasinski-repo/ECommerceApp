using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerceApp.Infrastructure.Migrations
{
    public partial class ChangesInContactDetailEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactDetails_Customers_CustomerId",
                table: "ContactDetails");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "ContactDetails",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ContactDetails_Customers_CustomerId",
                table: "ContactDetails",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactDetails_Customers_CustomerId",
                table: "ContactDetails");

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                table: "ContactDetails",
                type: "int",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AddForeignKey(
                name: "FK_ContactDetails_Customers_CustomerId",
                table: "ContactDetails",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
