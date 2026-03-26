using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Foundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceAliasAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "alias",
                table: "workspaces",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "workspaces",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tags",
                table: "workspaces",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_alias",
                table: "workspaces",
                column: "alias",
                unique: true,
                filter: "alias IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workspaces_alias",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "alias",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "workspaces");
        }
    }
}
