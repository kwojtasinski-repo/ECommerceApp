using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Sales.Orders.Migrations
{
    /// <inheritdoc />
    public partial class AddImageUrlToOrderItemSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "sales",
                table: "OrderItemSnapshots",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "sales",
                table: "OrderItemSnapshots");
        }
    }
}
