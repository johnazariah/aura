using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Migrations
{
    /// <inheritdoc />
    public partial class AddOrchestratorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentWave",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxParallelism",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrchestratorStatus",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TasksJson",
                table: "workflows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentWave",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "MaxParallelism",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "OrchestratorStatus",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "TasksJson",
                table: "workflows");
        }
    }
}
