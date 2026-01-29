using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Aura.Foundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "code_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    node_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    file_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    line_number = table.Column<int>(type: "integer", nullable: true),
                    signature = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    modifiers = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    repository_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    repository_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "index_metadata",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workspace_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    index_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    commit_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    files_indexed = table.Column<int>(type: "integer", nullable: false),
                    items_created = table.Column<int>(type: "integer", nullable: false),
                    stats = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_index_metadata", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rag_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    content_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_chunks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    canonical_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    git_remote_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    default_branch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "code_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    edge_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    properties = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_edges", x => x.id);
                    table.ForeignKey(
                        name: "FK_code_edges_code_nodes_source_id",
                        column: x => x.source_id,
                        principalTable: "code_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_code_edges_code_nodes_target_id",
                        column: x => x.target_id,
                        principalTable: "code_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_executions", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_executions_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tokens_used = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_rag_contexts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    query = table.Column<string>(type: "text", nullable: false),
                    content_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    chunk_content = table.Column<string>(type: "text", nullable: false),
                    score = table.Column<double>(type: "double precision", nullable: false),
                    source_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_rag_contexts", x => x.id);
                    table.ForeignKey(
                        name: "FK_message_rag_contexts_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_agent_id",
                table: "agent_executions",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_conversation_id",
                table: "agent_executions",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_started_at",
                table: "agent_executions",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_success",
                table: "agent_executions",
                column: "success");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_edge_type",
                table: "code_edges",
                column: "edge_type");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_source_id",
                table: "code_edges",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_source_id_edge_type",
                table: "code_edges",
                columns: new[] { "source_id", "edge_type" });

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_target_id",
                table: "code_edges",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_target_id_edge_type",
                table: "code_edges",
                columns: new[] { "target_id", "edge_type" });

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_full_name",
                table: "code_nodes",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_name",
                table: "code_nodes",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_node_type",
                table: "code_nodes",
                column: "node_type");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_repository_path",
                table: "code_nodes",
                column: "repository_path");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_repository_path_node_type",
                table: "code_nodes",
                columns: new[] { "repository_path", "node_type" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_agent_id",
                table: "conversations",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_created_at",
                table: "conversations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_index_metadata_index_type",
                table: "index_metadata",
                column: "index_type");

            migrationBuilder.CreateIndex(
                name: "IX_index_metadata_workspace_path",
                table: "index_metadata",
                column: "workspace_path");

            migrationBuilder.CreateIndex(
                name: "IX_index_metadata_workspace_path_index_type",
                table: "index_metadata",
                columns: new[] { "workspace_path", "index_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_message_rag_contexts_content_id",
                table: "message_rag_contexts",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_rag_contexts_message_id",
                table: "message_rag_contexts",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_created_at",
                table: "messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_content_id",
                table: "rag_chunks",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_content_id_chunk_index",
                table: "rag_chunks",
                columns: new[] { "content_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_canonical_path",
                table: "workspaces",
                column: "canonical_path",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_last_accessed_at",
                table: "workspaces",
                column: "last_accessed_at");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_status",
                table: "workspaces",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_executions");

            migrationBuilder.DropTable(
                name: "code_edges");

            migrationBuilder.DropTable(
                name: "index_metadata");

            migrationBuilder.DropTable(
                name: "message_rag_contexts");

            migrationBuilder.DropTable(
                name: "rag_chunks");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "code_nodes");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
