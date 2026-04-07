using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Sales.Orders.Migrations
{
    /// <inheritdoc />
    public partial class RenameImageUrlToImageIdInOrderItemSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImageId",
                schema: "sales",
                table: "OrderItemSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [sales].[OrderItemSnapshots] SET [ImageId] = TRY_CAST([ImageUrl] AS int) WHERE [ImageUrl] IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "sales",
                table: "OrderItemSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "sales",
                table: "OrderItemSnapshots",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [sales].[OrderItemSnapshots] SET [ImageUrl] = CAST([ImageId] AS nvarchar(2048)) WHERE [ImageId] IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "ImageId",
                schema: "sales",
                table: "OrderItemSnapshots");
        }
    }
}
