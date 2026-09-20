using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class PersistReceiptEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ReimbursementRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ReimbursementRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtractedFactsJson",
                table: "ReimbursementRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileSha256",
                table: "ReimbursementRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "ReimbursementRequests",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ReimbursementRequests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "ReimbursementRequests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ReimbursementRequests_FileSha256",
                table: "ReimbursementRequests",
                column: "FileSha256");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReimbursementRequests_FileSha256",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ExtractedFactsJson",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "FileSha256",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "ReimbursementRequests");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "ReimbursementRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);
        }
    }
}
