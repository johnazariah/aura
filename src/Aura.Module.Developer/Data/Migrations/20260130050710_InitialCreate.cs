using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    repository_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    worktree_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    git_branch = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    analyzed_context = table.Column<string>(type: "jsonb", nullable: true),
                    execution_plan = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pull_request_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    issue_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    issue_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    issue_number = table.Column<int>(type: "integer", nullable: true),
                    issue_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    issue_repo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    automation_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    source_guardian_id = table.Column<string>(type: "text", nullable: true),
                    pattern_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pattern_language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    suggested_capability = table.Column<string>(type: "text", nullable: true),
                    chat_history = table.Column<string>(type: "jsonb", nullable: true),
                    verification_passed = table.Column<bool>(type: "boolean", nullable: true),
                    verification_result = table.Column<string>(type: "text", nullable: true),
                    current_wave = table.Column<int>(type: "integer", nullable: false),
                    gate_mode = table.Column<int>(type: "integer", nullable: false),
                    gate_result = table.Column<string>(type: "text", nullable: true),
                    max_parallelism = table.Column<int>(type: "integer", nullable: false),
                    preferred_executor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "story_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    story_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    capability = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    input = table.Column<string>(type: "jsonb", nullable: true),
                    output = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approval = table.Column<int>(type: "integer", nullable: true),
                    approval_feedback = table.Column<string>(type: "text", nullable: true),
                    skip_reason = table.Column<string>(type: "text", nullable: true),
                    chat_history = table.Column<string>(type: "jsonb", nullable: true),
                    needs_rework = table.Column<bool>(type: "boolean", nullable: false),
                    previous_output = table.Column<string>(type: "jsonb", nullable: true),
                    wave = table.Column<int>(type: "integer", nullable: false),
                    executor_override = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_story_steps_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stories_created_at",
                table: "stories",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_stories_issue_url",
                table: "stories",
                column: "issue_url");

            migrationBuilder.CreateIndex(
                name: "IX_stories_status",
                table: "stories",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_story_steps_status",
                table: "story_steps",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_story_steps_story_id_order",
                table: "story_steps",
                columns: new[] { "story_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "story_steps");

            migrationBuilder.DropTable(
                name: "stories");
        }
    }
}
