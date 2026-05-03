using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Migrations
{
    /// <summary>
    /// Snapshot-only migration. No schema changes are applied.
    ///
    /// Context was changed from IdentityDbContext to DbContext as part of the IAM BC switch (ADR-0019).
    /// The Identity tables (iam.*) are now owned exclusively by IamDbContext.
    /// The orphaned FK constraints (FK_Customers_Users_UserId, FK_Orders_Users_UserId,
    /// FK_OrderItem_Users_UserId) and related indexes are retained in the database until
    /// the legacy Context is fully retired. This migration exists solely to bring the
    /// ContextModelSnapshot in sync with the current model and suppress the
    /// PendingModelChangesWarning on startup.
    /// </summary>
    public partial class RemoveIdentityFromLegacyContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - snapshot-only migration (see class header).
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - snapshot-only migration (see class header).
        }
    }
}