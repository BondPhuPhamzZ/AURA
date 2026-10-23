using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AURA.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedVisionProviderCooldown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VisionProviderCooldowns",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CooldownUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisionProviderCooldowns", x => x.Provider);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisionProviderCooldowns");
        }
    }
}
