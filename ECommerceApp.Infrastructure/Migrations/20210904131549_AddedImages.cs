using Microsoft.EntityFrameworkCore.Migrations;

namespace ECommerceApp.Infrastructure.Migrations
{
    public partial class AddedImages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePath = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    ItemId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Images_ItemId",
                table: "Images",
                column: "ItemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Administrator",
                column: "ConcurrencyStamp",
                value: "0db19580-f9b9-4924-9ad0-27916335b2a7");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Manager",
                column: "ConcurrencyStamp",
                value: "cba766b4-c4cc-4412-8e4e-9ade1d8e4146");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "NotRegister",
                column: "ConcurrencyStamp",
                value: "a4da4cc0-f7dc-4d75-a0c3-daea282b9429");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "Service",
                column: "ConcurrencyStamp",
                value: "7264c6b1-6682-42e6-8abc-d072b82ab15d");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "User",
                column: "ConcurrencyStamp",
                value: "4c10d949-d24e-448f-830d-2dc991e5ef2b");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8e445865-a24d-4543-a6c6-9443d048cdb9",
                column: "ConcurrencyStamp",
                value: "08d7858f-81cf-432d-bbcd-fe6a2a0c4944");
        }
    }
}
