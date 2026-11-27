using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrderFlow.Orchestrator.Persistence;

namespace OrderFlow.Orchestrator.Persistence.Migrations
{
    [DbContext(typeof(OrderSagaDbContext))]
    [Migration("20261127000000_AddTestMessagingTables")]
    public sealed class AddTestMessagingTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Install the test tables alongside the regular tables. The EF snapshot describes
            // the default model, and the service selects the test mappings at runtime.
            migrationBuilder.EnsureSchema("test");

            migrationBuilder.CreateTable(
                name: "OrderSagaStates",
                schema: "test",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderSagaStates", x => x.CorrelationId);
                });

            migrationBuilder.CreateTable(
                name: "SagaInboxState",
                schema: "test",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaInboxState", x => x.Id);
                    table.UniqueConstraint("AK_SagaInboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "SagaOutboxState",
                schema: "test",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaOutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "SagaOutboxMessage",
                schema: "test",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnqueueTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SagaOutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_SagaOutboxMessage_SagaInboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "test",
                        principalTable: "SagaInboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_SagaOutboxMessage_SagaOutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "test",
                        principalTable: "SagaOutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SagaInboxState_Delivered",
                table: "SagaInboxState",
                schema: "test",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_SagaOutboxMessage_EnqueueTime",
                table: "SagaOutboxMessage",
                schema: "test",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_SagaOutboxMessage_ExpirationTime",
                table: "SagaOutboxMessage",
                schema: "test",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_SagaOutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "SagaOutboxMessage",
                schema: "test",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true,
                filter: "[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SagaOutboxMessage_OutboxId_SequenceNumber",
                table: "SagaOutboxMessage",
                schema: "test",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true,
                filter: "[OutboxId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SagaOutboxState_Created",
                table: "SagaOutboxState",
                schema: "test",
                column: "Created");

            migrationBuilder.AddColumn<int>(
                name: "InventoryAttempts",
                table: "OrderSagaStates",
                schema: "test",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "InventoryDeadlineUtc",
                table: "OrderSagaStates",
                schema: "test",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InventoryRecoveryReason",
                table: "OrderSagaStates",
                schema: "test",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderSagaStates_InventoryDeadlineUtc",
                table: "OrderSagaStates",
                schema: "test",
                column: "InventoryDeadlineUtc",
                filter: "[InventoryDeadlineUtc] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SagaOutboxMessage", schema: "test");
            migrationBuilder.DropTable(name: "SagaOutboxState", schema: "test");
            migrationBuilder.DropTable(name: "SagaInboxState", schema: "test");
            migrationBuilder.DropTable(name: "OrderSagaStates", schema: "test");
        }
    }
}
