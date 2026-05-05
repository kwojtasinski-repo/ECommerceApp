using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Identity.IAM.Migrations
{
    /// <summary>
    /// Seeds the admin user and their Administrator role assignment into the [iam] schema.
    /// Data is migrated from the legacy [dbo].[AspNetUsers] / [dbo].[AspNetUserRoles] tables.
    /// Passwords and security stamps are preserved as-is from the legacy context.
    /// </summary>
    public partial class SeedUsersAndUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "iam",
                table: "Users",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { "8e445865-a24d-4543-a6c6-9443d048cdb9", 0, "a2827a1e-8a9d-4399-926f-f4402641d5bc", "admin@localhost", true, false, null, "ADMIN@LOCALHOST", "ADMIN@LOCALHOST", "AQAAAAIAAYagAAAAEJxfXHqx0VsqfFg4w9HgPGiY3GPy1FxpIrwtoUmrzw2hphdeY1CvTHct5xRTzKq+mw==", null, false, "KZIQWXZBKO2J2CM6W7T75P33JA7VRCR6", false, "admin@localhost" });

            migrationBuilder.InsertData(
                schema: "iam",
                table: "UserRoles",
                columns: new[] { "RoleId", "UserId" },
                values: new object[] { "Administrator", "8e445865-a24d-4543-a6c6-9443d048cdb9" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "iam",
                table: "UserRoles",
                keyColumns: new[] { "RoleId", "UserId" },
                keyValues: new object[] { "Administrator", "8e445865-a24d-4543-a6c6-9443d048cdb9" });

            migrationBuilder.DeleteData(
                schema: "iam",
                table: "Users",
                keyColumn: "Id",
                keyValue: "8e445865-a24d-4543-a6c6-9443d048cdb9");
        }
    }
}
