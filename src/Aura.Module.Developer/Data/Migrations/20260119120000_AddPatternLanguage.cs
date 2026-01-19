using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations;

/// <inheritdoc />
public partial class AddPatternLanguage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "pattern_name",
            table: "workflows",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "pattern_language",
            table: "workflows",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "pattern_name",
            table: "workflows");

        migrationBuilder.DropColumn(
            name: "pattern_language",
            table: "workflows");
    }
}
