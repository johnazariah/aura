using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "issue_number",
                table: "workflows",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issue_owner",
                table: "workflows",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issue_provider",
                table: "workflows",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issue_repo",
                table: "workflows",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issue_url",
                table: "workflows",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "workflows",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_issue_url",
                table: "workflows",
                column: "issue_url");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflows_issue_url",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "issue_number",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "issue_owner",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "issue_provider",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "issue_repo",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "issue_url",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "workflows");
        }
    }
}
