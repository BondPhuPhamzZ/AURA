using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptRetentionDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RetentionExpiresAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);

            // Backfill only requests whose final decision is already recorded. Open
            // escalations, pending/processing work and retryable failures stay null.
            migrationBuilder.Sql("""
                UPDATE [dbo].[ReimbursementRequests]
                SET [RetentionExpiresAt] = DATEADD(day, 7,
                    CASE WHEN [ManagerAnswer] IS NOT NULL AND [ManagerDecisionAt] IS NOT NULL
                         THEN [ManagerDecisionAt] ELSE [ProcessingCompletedAt] END)
                WHERE ([ManagerAnswer] IS NOT NULL AND [ManagerDecisionAt] IS NOT NULL
                       AND [Status] IN (
                           'MANUAL_REVIEW_ACCEPTED', 'RETURNED_FOR_MORE_EVIDENCE',
                           'APPROVED_POLICY_EXCEPTION', 'REJECTED_POLICY_EXCEPTION',
                           'FORWARDED_TO_AUTHORITY', 'RETURNED_BY_MANAGER', 'RETURNED_FOR_RETRY'))
                   OR ([ProcessingState] = 'COMPLETED' AND [Status] = 'AUTO_APPROVE'
                       AND [ProcessingCompletedAt] IS NOT NULL);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementRequests_RetentionExpiresAt",
                table: "ReimbursementRequests",
                column: "RetentionExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReimbursementRequests_RetentionExpiresAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "RetentionExpiresAt",
                table: "ReimbursementRequests");
        }
    }
}
