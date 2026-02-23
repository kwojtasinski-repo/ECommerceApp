using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.AccountProfile.Migrations
{
    // Intent: Change the ZipCode column in the Addresses table from int to nvarchar(12) to support
    // international ZIP codes via the new string-based ZipCode value object.
    // Data impact: existing int ZIP codes (e.g. 12345) will be converted to their string representation
    // by SQL Server during the ALTER COLUMN. No data loss for valid positive integers. Down migration
    // reverts to int — any non-numeric ZIP values stored after this migration will fail on rollback.
    /// <inheritdoc />
    public partial class AddressZipCodeColumnToString : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ZipCode",
                schema: "profile",
                table: "Addresses",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ZipCode",
                schema: "profile",
                table: "Addresses",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12);
        }
    }
}
