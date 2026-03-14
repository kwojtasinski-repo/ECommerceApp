using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Supporting.Currencies.Migrations
{
    /// <inheritdoc />
    public partial class SeedPlnCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [currencies].[Currencies] WHERE [Code] = 'PLN')
                BEGIN
                    SET IDENTITY_INSERT [currencies].[Currencies] ON;
                    INSERT INTO [currencies].[Currencies] ([Id], [Code], [Description])
                    VALUES (1, 'PLN', 'Polski złoty');
                    SET IDENTITY_INSERT [currencies].[Currencies] OFF;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [currencies].[Currencies] WHERE [Id] = 1 AND [Code] = 'PLN';
                """);
        }
    }
}
