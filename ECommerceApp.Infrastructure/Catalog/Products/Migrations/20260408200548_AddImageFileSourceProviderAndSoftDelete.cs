using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Catalog.Products.Migrations
{
    /// <inheritdoc />
    public partial class AddImageFileSourceProviderAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileSource",
                schema: "catalog",
                table: "Images",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "catalog",
                table: "Images",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "catalog",
                table: "Images",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
                UPDATE [catalog].[Images]
                SET
                    [FileSource] = CASE
                        WHEN CHARINDEX('\', [FileName]) > 0
                            THEN LEFT([FileName], LEN([FileName]) - CHARINDEX('\', REVERSE([FileName])))
                        WHEN CHARINDEX('/', [FileName]) > 0
                            THEN LEFT([FileName], LEN([FileName]) - CHARINDEX('/', REVERSE([FileName])))
                        ELSE ''
                    END,
                    [FileName] = CASE
                        WHEN CHARINDEX('\', [FileName]) > 0
                            THEN RIGHT([FileName], CHARINDEX('\', REVERSE([FileName])) - 1)
                        WHEN CHARINDEX('/', [FileName]) > 0
                            THEN RIGHT([FileName], CHARINDEX('/', REVERSE([FileName])) - 1)
                        ELSE [FileName]
                    END,
                    [Provider] = 'Local'
                WHERE CHARINDEX('\', [FileName]) > 0 OR CHARINDEX('/', [FileName]) > 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileSource",
                schema: "catalog",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "catalog",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "catalog",
                table: "Images");
        }
    }
}
