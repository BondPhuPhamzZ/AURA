using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ForwardedAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForwardedToManager",
                table: "ReimbursementRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ManagerAnswer",
                table: "ReimbursementRequests",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagerDecisionAt",
                table: "ReimbursementRequests",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForwardedAt",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "IsForwardedToManager",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ManagerAnswer",
                table: "ReimbursementRequests");

            migrationBuilder.DropColumn(
                name: "ManagerDecisionAt",
                table: "ReimbursementRequests");
        }
    }
}
