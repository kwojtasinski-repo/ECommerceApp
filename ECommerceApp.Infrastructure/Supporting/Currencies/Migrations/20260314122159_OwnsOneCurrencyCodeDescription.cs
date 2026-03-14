using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Migrations
{
    /// <inheritdoc />
    public partial class OwnsOneCurrencyCodeDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Currencies_Code",
                schema: "currencies",
                table: "Currencies");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                schema: "currencies",
                table: "Currencies",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Currencies_Code",
                schema: "currencies",
                table: "Currencies");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Code",
                schema: "currencies",
                table: "Currencies",
                column: "Code",
                unique: true);
        }
    }
}
