using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TasksJson",
                table: "workflows",
                newName: "GateResult");

            migrationBuilder.RenameColumn(
                name: "OrchestratorStatus",
                table: "workflows",
                newName: "GateMode");

            migrationBuilder.AddColumn<int>(
                name: "Wave",
                table: "workflow_steps",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Wave",
                table: "workflow_steps");

            migrationBuilder.RenameColumn(
                name: "GateResult",
                table: "workflows",
                newName: "TasksJson");

            migrationBuilder.RenameColumn(
                name: "GateMode",
                table: "workflows",
                newName: "OrchestratorStatus");
        }
    }
}
