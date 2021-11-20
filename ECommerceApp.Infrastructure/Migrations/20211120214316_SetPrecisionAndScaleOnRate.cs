using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerceApp.Infrastructure.Migrations
{
    public partial class SetPrecisionAndScaleOnRate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "CurrencyRates",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Administrator",
                column: "ConcurrencyStamp",
                value: "214260fe-916d-4419-b506-b83b6077bcc2");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Manager",
                column: "ConcurrencyStamp",
                value: "ce41c70b-6ee7-4af6-bf95-c8c8717cf47c");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "NotRegister",
                column: "ConcurrencyStamp",
                value: "f55e0d07-b280-4030-95f1-9f1e0f781b14");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Service",
                column: "ConcurrencyStamp",
                value: "3a8d6465-fd51-4388-ab7a-36d19fd95951");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "User",
                column: "ConcurrencyStamp",
                value: "2c1b5fef-1cbe-4139-bffc-9f67449870b1");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e445865-a24d-4543-a6c6-9443d048cdb9",
                column: "ConcurrencyStamp",
                value: "7a3d5a43-ac06-4e9b-ad6f-b82beec8b450");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Rate",
                table: "CurrencyRates",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Administrator",
                column: "ConcurrencyStamp",
                value: "f7cfafb7-0798-4b6a-9387-90586f7b22f1");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Manager",
                column: "ConcurrencyStamp",
                value: "e17981cc-2875-4b5d-8e94-fe3d3c170303");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "NotRegister",
                column: "ConcurrencyStamp",
                value: "971bdc0a-851e-4f67-a32c-d012cb182c13");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Service",
                column: "ConcurrencyStamp",
                value: "b4e347e7-9942-47ee-9e84-08421fa9d71e");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "User",
                column: "ConcurrencyStamp",
                value: "4c6399c7-63aa-47c7-816a-8cd6cbbcc24b");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e445865-a24d-4543-a6c6-9443d048cdb9",
                column: "ConcurrencyStamp",
                value: "e044ea80-eea5-4500-9cc4-03f180e68915");
        }
    }
}
