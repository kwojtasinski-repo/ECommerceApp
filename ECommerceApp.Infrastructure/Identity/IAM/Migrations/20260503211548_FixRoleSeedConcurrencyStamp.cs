using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Identity.IAM.Migrations
{
    /// <summary>
    /// Pins ConcurrencyStamp on the role seed data to static hardcoded values so EF model
    /// building is deterministic (fixes PendingModelChangesWarning / NonDeterministicModel).
    ///
    /// Root cause: IdentityRole() constructor sets ConcurrencyStamp = Guid.NewGuid(), making
    /// the HasData seed non-deterministic each build.
    ///
    /// Up(): UPDATE each role row in [iam].[Roles] to the fixed stamp value. Safe on existing
    /// DBs (roles already present from legacy Context seed) and on fresh DBs (InitIamSchema
    /// inserts them first with the same stamps, so this is a no-op update).
    /// </summary>
    public partial class FixRoleSeedConcurrencyStamp : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ASP.NET Core Identity 10 reduced UserLogin and UserToken PK key columns from
            // nvarchar(450) → nvarchar(128). InitIamSchema was generated targeting an older
            // Identity version and created them as 450. Alter them here so the DB matches the
            // current model.  SQL Server allows narrowing a NOT NULL column in a PK without
            // dropping/recreating the constraint.
            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "iam",
                table: "UserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderKey",
                schema: "iam",
                table: "UserLogins",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "LoginProvider",
                schema: "iam",
                table: "UserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "iam",
                table: "UserTokens",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            // Pin ConcurrencyStamp on seed roles to static values (non-deterministic Guid fix).
            migrationBuilder.Sql("UPDATE [iam].[Roles] SET ConcurrencyStamp = 'a1b2c3d4-0001-0000-0000-000000000000' WHERE Id = 'Administrator'");
            migrationBuilder.Sql("UPDATE [iam].[Roles] SET ConcurrencyStamp = 'a1b2c3d4-0002-0000-0000-000000000000' WHERE Id = 'Manager'");
            migrationBuilder.Sql("UPDATE [iam].[Roles] SET ConcurrencyStamp = 'a1b2c3d4-0003-0000-0000-000000000000' WHERE Id = 'Service'");
            migrationBuilder.Sql("UPDATE [iam].[Roles] SET ConcurrencyStamp = 'a1b2c3d4-0004-0000-0000-000000000000' WHERE Id = 'User'");
            migrationBuilder.Sql("UPDATE [iam].[Roles] SET ConcurrencyStamp = 'a1b2c3d4-0005-0000-0000-000000000000' WHERE Id = 'NotRegister'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — snapshot-only migration (see class header).
        }
    }
}
