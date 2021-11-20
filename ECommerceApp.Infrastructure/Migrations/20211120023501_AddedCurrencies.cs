using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerceApp.Infrastructure.Migrations
{
    public partial class AddedCurrencies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Payments",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Orders",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Items",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(maxLength: 3, nullable: false),
                    Description = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyRates",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CurrencyId = table.Column<int>(nullable: false),
                    Rate = table.Column<decimal>(nullable: false),
                    CurrencyDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurrencyRates_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "Description" },
                values: new object[] { 1, "PLN", "Polski złoty" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CurrencyId",
                table: "Payments",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CurrencyId",
                table: "Orders",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CurrencyId",
                table: "Items",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                table: "Currencies",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyRates_CurrencyId",
                table: "CurrencyRates",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Currencies_CurrencyId",
                table: "Items",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Currencies_CurrencyId",
                table: "Orders",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Currencies_CurrencyId",
                table: "Payments",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_Currencies_CurrencyId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Currencies_CurrencyId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Currencies_CurrencyId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "CurrencyRates");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CurrencyId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CurrencyId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Items_CurrencyId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Items");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Administrator",
                column: "ConcurrencyStamp",
                value: "a40e09b6-86b9-4f0e-a100-c72097602be9");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Manager",
                column: "ConcurrencyStamp",
                value: "576407b1-7db3-4f07-aa0d-534b7edf8475");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "NotRegister",
                column: "ConcurrencyStamp",
                value: "b10bac1c-ee97-47ce-8efd-af2dba13f2dd");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Service",
                column: "ConcurrencyStamp",
                value: "75aa4501-4fff-4a05-b4c1-5d7aab195ec3");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "User",
                column: "ConcurrencyStamp",
                value: "f3f9f13d-8999-4bde-96c3-7cd740bebae6");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e445865-a24d-4543-a6c6-9443d048cdb9",
                column: "ConcurrencyStamp",
                value: "6f285ab8-0eff-4138-9074-ac41bbec7f20");
        }
    }
}
