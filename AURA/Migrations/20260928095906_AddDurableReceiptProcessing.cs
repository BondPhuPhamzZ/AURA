using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableReceiptProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DuplicateDetected",
                table: "ReimbursementRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DuplicatePolicyEnabled",
                table: "ReimbursementRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FallbackUsed",
                table: "ReimbursementRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryProvider",
                table: "ReimbursementRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingAttemptCount",
                table: "ReimbursementRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingCompletedAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingLeaseUntil",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessingStartedAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingState",
                table: "ReimbursementRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "COMPLETED");

            migrationBuilder.AddColumn<string>(
                name: "ProviderErrorCode",
                table: "ReimbursementRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "ServedProvider",
                table: "ReimbursementRequests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitterCode",
                table: "ReimbursementRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "LEGACY");

            migrationBuilder.AddColumn<string>(
                name: "SubmitterDepartment",
                table: "ReimbursementRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Không xác định");

            migrationBuilder.AddColumn<string>(
                name: "SubmitterDisplayName",
                table: "ReimbursementRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "Dữ liệu trước migration");

            migrationBuilder.Sql(
                "UPDATE [ReimbursementRequests] SET [QueuedAt] = [CreatedAt], " +
                "[ProcessingCompletedAt] = [CreatedAt] WHERE [ProcessingState] = 'COMPLETED'");

            migrationBuilder.AlterColumn<string>(
                name: "RequestId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementRequests_ProcessingState_QueuedAt",
                table: "ReimbursementRequests",
                columns: new[] { "ProcessingState", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RequestId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "RequestId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReimbursementRequests_ProcessingState_QueuedAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_RequestId_Timestamp",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DuplicateDetected",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "DuplicatePolicyEnabled",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "FallbackUsed",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "PrimaryProvider",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProcessingAttemptCount",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProcessingCompletedAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseUntil",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProcessingStartedAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProcessingState",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ProviderErrorCode",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ServedProvider",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "SubmitterCode",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "SubmitterDepartment",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "SubmitterDisplayName",
                table: "ReimbursementRequests");

            migrationBuilder.AlterColumn<string>(
                name: "RequestId",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
