using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceApp.Infrastructure.Sagas.Migrations
{
    /// <inheritdoc />
    public partial class InitSagasSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sagas");

            migrationBuilder.CreateTable(
                name: "SagaInstances",
                schema: "sagas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SagaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SagaSteps",
                schema: "sagas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SagaInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SagaSteps_SagaInstances_SagaInstanceId",
                        column: x => x.SagaInstanceId,
                        principalSchema: "sagas",
                        principalTable: "SagaInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaInstances_SagaType_CorrelationId",
                schema: "sagas",
                table: "SagaInstances",
                columns: new[] { "SagaType", "CorrelationId" });

            migrationBuilder.CreateIndex(
                name: "IX_SagaSteps_SagaInstanceId_StepName",
                schema: "sagas",
                table: "SagaSteps",
                columns: new[] { "SagaInstanceId", "StepName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SagaSteps",
                schema: "sagas");

            migrationBuilder.DropTable(
                name: "SagaInstances",
                schema: "sagas");
        }
    }
}
