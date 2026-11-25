using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Orchestrator.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InventoryAttempts",
                table: "OrderSagaStates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InventoryDeadlineUtc",
                table: "OrderSagaStates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryRecoveryReason",
                table: "OrderSagaStates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [OrderSagaStates]
                SET [InventoryAttempts] = 1,
                    [InventoryDeadlineUtc] = DATEADD(second, 15, SYSUTCDATETIME())
                WHERE [CurrentState] IN ('AwaitingInventory', 'ReleasingInventory')
                """);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSagaStates_InventoryDeadlineUtc",
                table: "OrderSagaStates",
                column: "InventoryDeadlineUtc",
                filter: "[InventoryDeadlineUtc] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderSagaStates_InventoryDeadlineUtc",
                table: "OrderSagaStates");

            migrationBuilder.DropColumn(
                name: "InventoryAttempts",
                table: "OrderSagaStates");

            migrationBuilder.DropColumn(
                name: "InventoryDeadlineUtc",
                table: "OrderSagaStates");

            migrationBuilder.DropColumn(
                name: "InventoryRecoveryReason",
                table: "OrderSagaStates");
        }
    }
}
