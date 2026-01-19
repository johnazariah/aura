using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "VerificationPassed",
                table: "workflows",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationResult",
                table: "workflows",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationPassed",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "VerificationResult",
                table: "workflows");
        }
    }
}
