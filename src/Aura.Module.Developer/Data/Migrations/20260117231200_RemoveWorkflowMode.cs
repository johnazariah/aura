using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkflowMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "mode",
                table: "workflows");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "workflows",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Structured");
        }
    }
}
