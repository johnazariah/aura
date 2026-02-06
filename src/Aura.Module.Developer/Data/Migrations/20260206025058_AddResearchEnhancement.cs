using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResearchEnhancement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identified_risks",
                table: "stories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "open_questions",
                table: "stories",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identified_risks",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "open_questions",
                table: "stories");
        }
    }
}
