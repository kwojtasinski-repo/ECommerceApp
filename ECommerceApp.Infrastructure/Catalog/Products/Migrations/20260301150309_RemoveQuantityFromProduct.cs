using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Catalog.Products.Migrations
{
    /// <summary>
    /// Removes the Quantity column from catalog.Products.
    /// Quantity tracking belongs to an Inventory BC, not the Catalog BC.
    /// Data impact: the Quantity column and all stored values are permanently dropped.
    /// Rollback: run 'ef migrations remove' before applying to production, or apply Down() which re-adds the column with default value 0.
    /// </summary>
    public partial class RemoveQuantityFromProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "catalog",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                schema: "catalog",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
