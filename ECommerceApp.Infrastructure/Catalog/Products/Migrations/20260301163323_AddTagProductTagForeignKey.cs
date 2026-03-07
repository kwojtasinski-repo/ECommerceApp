using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Catalog.Products.Migrations
{
    /// <summary>
    /// Adds <c>FK_ProductTags_Tags_TagId</c> (CASCADE DELETE) to <c>catalog.ProductTags</c>
    /// so that deleting a <c>Tag</c> automatically removes its join rows in <c>ProductTags</c>.
    /// Data impact: schema-only — no row data is changed.
    /// Rollback: <c>Down()</c> drops the foreign key; the join rows remain intact.
    /// </summary>
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
