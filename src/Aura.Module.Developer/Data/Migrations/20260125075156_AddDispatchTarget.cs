using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DispatchTarget",
                table: "workflows",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DispatchTarget",
                table: "workflows");
        }
    }
}
