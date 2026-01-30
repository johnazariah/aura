using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Foundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceIdToRagChunk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "workspace_id",
                table: "rag_chunks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_workspace_id",
                table: "rag_chunks",
                column: "workspace_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rag_chunks_workspace_id",
                table: "rag_chunks");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "rag_chunks");
        }
    }
}
