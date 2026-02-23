using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.AccountProfile.Migrations
{
    /// <summary>
    /// Creates the [profile] schema with two tables: UserProfiles and Addresses (OwnsMany).
    ///
    /// Intent: establishes the AccountProfile bounded-context database boundary managed by
    /// UserProfileDbContext — separate from the legacy [dbo] tables in Context (ADR-0005).
    /// This is a CREATE-only migration — no existing data is touched.
    ///
    /// Rollback: apply Down() which drops [profile].[Addresses] then [profile].[UserProfiles]
    /// and leaves all other schemas untouched.
    /// </summary>
    public partial class InitProfileSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "profile");

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IsCompany = table.Column<bool>(type: "bit", nullable: false),
                    NIP = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanyName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                schema: "profile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Street = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BuildingNumber = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FlatNumber = table.Column<int>(type: "int", nullable: true),
                    ZipCode = table.Column<int>(type: "int", nullable: false),
                    City = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UserProfileId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_UserProfiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalSchema: "profile",
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_UserProfileId",
                schema: "profile",
                table: "Addresses",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_ZipCode",
                schema: "profile",
                table: "Addresses",
                column: "ZipCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_NIP",
                schema: "profile",
                table: "UserProfiles",
                column: "NIP");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                schema: "profile",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "profile");

            migrationBuilder.DropTable(
                name: "UserProfiles",
                schema: "profile");
        }
    }
}
