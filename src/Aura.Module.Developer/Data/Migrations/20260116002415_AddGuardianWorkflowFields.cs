using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceGuardianId",
                table: "workflows",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedCapability",
                table: "workflows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "SourceGuardianId",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "SuggestedCapability",
                table: "workflows");
        }
    }
}
