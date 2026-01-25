using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWaveAndGateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GateMode",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GateResult",
                table: "workflows",
                type: "text",
                nullable: true);

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
                name: "GateMode",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "GateResult",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "Wave",
                table: "workflow_steps");
        }
    }
}
