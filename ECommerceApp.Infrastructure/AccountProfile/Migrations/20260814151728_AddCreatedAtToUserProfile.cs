using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.AccountProfile.Migrations
{
    /// <summary>
    /// Adds UserProfile.CreatedAt (ADR-0030 Phase 4, guest-checkout unclaimed-profile cleanup job).
    /// Additive only: nullable-at-the-database-engine-level via defaultValueSql so existing rows are
    /// backfilled to the migration's apply time (not treated as ancient), avoiding an immediate mass
    /// deletion by the new retention job on first run. No data loss, no destructive change.
    /// </summary>
    public partial class AddCreatedAtToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "profile",
                table: "UserProfiles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_CreatedAt",
                schema: "profile",
                table: "UserProfiles",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_CreatedAt",
                schema: "profile",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "profile",
                table: "UserProfiles");
        }
    }
}
