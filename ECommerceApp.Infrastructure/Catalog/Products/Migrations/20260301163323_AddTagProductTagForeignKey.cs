using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Catalog.Products.Migrations
{
    /// <inheritdoc />
    // Adds FK_ProductTags_Tags_TagId (CASCADE) so deleting a Tag automatically removes its ProductTag join rows.
    public partial class AddTagProductTagForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ProductTags_Tags_TagId",
                schema: "catalog",
                table: "ProductTags",
                column: "TagId",
                principalSchema: "catalog",
                principalTable: "Tags",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductTags_Tags_TagId",
                schema: "catalog",
                table: "ProductTags");
        }
    }
}
