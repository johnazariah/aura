using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Module.Developer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflows",
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
                    Source = table.Column<int>(type: "integer", nullable: false),
                    SourceGuardianId = table.Column<string>(type: "text", nullable: true),
                    pattern_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pattern_language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    SuggestedCapability = table.Column<string>(type: "text", nullable: true),
                    chat_history = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    previous_output = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_workflow_steps_workflows_workflow_id",
                        column: x => x.workflow_id,
                        principalTable: "workflows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_status",
                table: "workflow_steps",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_steps_workflow_id_order",
                table: "workflow_steps",
                columns: new[] { "workflow_id", "order" });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_created_at",
                table: "workflows",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_issue_url",
                table: "workflows",
                column: "issue_url");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_status",
                table: "workflows",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_steps");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
