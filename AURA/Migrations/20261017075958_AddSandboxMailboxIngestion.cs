using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class AddSandboxMailboxIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceReference",
                table: "ReimbursementRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "ReimbursementRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MANUAL_UPLOAD");

            migrationBuilder.CreateTable(
                name: "IngestionOutboxEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngestionOutboxEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailboxConnections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalMailboxId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AllowedFolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MailboxConnectionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExternalThreadId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Sender = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceFolder = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessages_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailboxSyncCursors",
                columns: table => new
                {
                    MailboxConnectionId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSuccessfulSyncAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsecutiveFailureCount = table.Column<int>(type: "int", nullable: false),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailboxSyncCursors", x => x.MailboxConnectionId);
                    table.ForeignKey(
                        name: "FK_MailboxSyncCursors_MailboxConnections_MailboxConnectionId",
                        column: x => x.MailboxConnectionId,
                        principalTable: "MailboxConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailAttachments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmailMessageId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalAttachmentId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    QuarantineStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuarantineReasonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    QuarantineReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReimbursementRequestId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachments_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailAttachments_ReimbursementRequests_ReimbursementRequestId",
                        column: x => x.ReimbursementRequestId,
                        principalTable: "ReimbursementRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementRequests_SourceType",
                table: "ReimbursementRequests",
                column: "SourceType");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachments_EmailMessageId_ExternalAttachmentId",
                table: "EmailAttachments",
                columns: new[] { "EmailMessageId", "ExternalAttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachments_FileSha256",
                table: "EmailAttachments",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAttachments_ReimbursementRequestId",
                table: "EmailAttachments",
                column: "ReimbursementRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_MailboxConnectionId_ExternalMessageId",
                table: "EmailMessages",
                columns: new[] { "MailboxConnectionId", "ExternalMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngestionOutboxEvents_ProcessedAt_OccurredAt",
                table: "IngestionOutboxEvents",
                columns: new[] { "ProcessedAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MailboxConnections_Provider_ExternalMailboxId",
                table: "MailboxConnections",
                columns: new[] { "Provider", "ExternalMailboxId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAttachments");

            migrationBuilder.DropTable(
                name: "IngestionOutboxEvents");

            migrationBuilder.DropTable(
                name: "MailboxSyncCursors");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "MailboxConnections");

            migrationBuilder.DropIndex(
                name: "IX_ReimbursementRequests_SourceType",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "SourceReference",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ReimbursementRequests");
        }
    }
}
