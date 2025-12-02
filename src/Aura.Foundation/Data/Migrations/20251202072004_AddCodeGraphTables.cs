using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Aura.Foundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeGraphTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_code_edges_code_nodes_source_node_id",
                table: "code_edges");

            migrationBuilder.DropForeignKey(
                name: "fk_code_edges_code_nodes_target_node_id",
                table: "code_edges");

            migrationBuilder.DropForeignKey(
                name: "fk_messages_conversations_conversation_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropPrimaryKey(
                name: "pk_messages",
                table: "messages");

            migrationBuilder.DropPrimaryKey(
                name: "pk_conversations",
                table: "conversations");

            migrationBuilder.DropPrimaryKey(
                name: "pk_code_nodes",
                table: "code_nodes");

            migrationBuilder.DropIndex(
                name: "ix_code_nodes_fully_qualified_name",
                table: "code_nodes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_code_edges",
                table: "code_edges");

            migrationBuilder.DropPrimaryKey(
                name: "pk_agent_executions",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "metadata",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "fully_qualified_name",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "source_code",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "code_edges");

            migrationBuilder.DropColumn(
                name: "duration",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "input",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "output",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "status",
                table: "agent_executions");

            migrationBuilder.RenameIndex(
                name: "ix_messages_conversation_id",
                table: "messages",
                newName: "IX_messages_conversation_id");

            migrationBuilder.RenameIndex(
                name: "ix_conversations_created_at",
                table: "conversations",
                newName: "IX_conversations_created_at");

            migrationBuilder.RenameColumn(
                name: "updated_at",
                table: "code_nodes",
                newName: "indexed_at");

            migrationBuilder.RenameIndex(
                name: "ix_code_nodes_node_type",
                table: "code_nodes",
                newName: "IX_code_nodes_node_type");

            migrationBuilder.RenameIndex(
                name: "ix_code_nodes_name",
                table: "code_nodes",
                newName: "IX_code_nodes_name");

            migrationBuilder.RenameColumn(
                name: "target_node_id",
                table: "code_edges",
                newName: "target_id");

            migrationBuilder.RenameColumn(
                name: "source_node_id",
                table: "code_edges",
                newName: "source_id");

            migrationBuilder.RenameColumn(
                name: "relationship_type",
                table: "code_edges",
                newName: "edge_type");

            migrationBuilder.RenameIndex(
                name: "ix_code_edges_target_node_id",
                table: "code_edges",
                newName: "IX_code_edges_target_id");

            migrationBuilder.RenameIndex(
                name: "ix_code_edges_source_node_id",
                table: "code_edges",
                newName: "IX_code_edges_source_id");

            migrationBuilder.RenameIndex(
                name: "ix_code_edges_relationship_type",
                table: "code_edges",
                newName: "IX_code_edges_edge_type");

            migrationBuilder.RenameColumn(
                name: "error",
                table: "agent_executions",
                newName: "response");

            migrationBuilder.RenameColumn(
                name: "ended_at",
                table: "agent_executions",
                newName: "completed_at");

            migrationBuilder.RenameIndex(
                name: "ix_agent_executions_started_at",
                table: "agent_executions",
                newName: "IX_agent_executions_started_at");

            migrationBuilder.RenameIndex(
                name: "ix_agent_executions_agent_id",
                table: "agent_executions",
                newName: "IX_agent_executions_agent_id");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "messages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "messages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tokens_used",
                table: "messages",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "conversations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_id",
                table: "conversations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "workspace_path",
                table: "conversations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "code_nodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "code_nodes",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "code_nodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modifiers",
                table: "code_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature",
                table: "code_nodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "workspace_path",
                table: "code_nodes",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "code_edges",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "conversation_id",
                table: "agent_executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "duration_ms",
                table: "agent_executions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "agent_executions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "model",
                table: "agent_executions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prompt",
                table: "agent_executions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "agent_executions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "success",
                table: "agent_executions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_messages",
                table: "messages",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_conversations",
                table: "conversations",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_code_nodes",
                table: "code_nodes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_code_edges",
                table: "code_edges",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_agent_executions",
                table: "agent_executions",
                column: "id");

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

            migrationBuilder.CreateIndex(
                name: "IX_messages_created_at",
                table: "messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_agent_id",
                table: "conversations",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_full_name",
                table: "code_nodes",
                column: "full_name");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_workspace_path",
                table: "code_nodes",
                column: "workspace_path");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_workspace_path_node_type",
                table: "code_nodes",
                columns: new[] { "workspace_path", "node_type" });

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_source_id_edge_type",
                table: "code_edges",
                columns: new[] { "source_id", "edge_type" });

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_target_id_edge_type",
                table: "code_edges",
                columns: new[] { "target_id", "edge_type" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_conversation_id",
                table: "agent_executions",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_executions_success",
                table: "agent_executions",
                column: "success");

            migrationBuilder.CreateIndex(
                name: "IX_message_rag_contexts_content_id",
                table: "message_rag_contexts",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "IX_message_rag_contexts_message_id",
                table: "message_rag_contexts",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_content_id",
                table: "rag_chunks",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "IX_rag_chunks_content_id_chunk_index",
                table: "rag_chunks",
                columns: new[] { "content_id", "chunk_index" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_executions_conversations_conversation_id",
                table: "agent_executions",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_code_edges_code_nodes_source_id",
                table: "code_edges",
                column: "source_id",
                principalTable: "code_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_code_edges_code_nodes_target_id",
                table: "code_edges",
                column: "target_id",
                principalTable: "code_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_messages_conversations_conversation_id",
                table: "messages",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_executions_conversations_conversation_id",
                table: "agent_executions");

            migrationBuilder.DropForeignKey(
                name: "FK_code_edges_code_nodes_source_id",
                table: "code_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_code_edges_code_nodes_target_id",
                table: "code_edges");

            migrationBuilder.DropForeignKey(
                name: "FK_messages_conversations_conversation_id",
                table: "messages");

            migrationBuilder.DropTable(
                name: "message_rag_contexts");

            migrationBuilder.DropTable(
                name: "rag_chunks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_messages",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "IX_messages_created_at",
                table: "messages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_conversations",
                table: "conversations");

            migrationBuilder.DropIndex(
                name: "IX_conversations_agent_id",
                table: "conversations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_code_nodes",
                table: "code_nodes");

            migrationBuilder.DropIndex(
                name: "IX_code_nodes_full_name",
                table: "code_nodes");

            migrationBuilder.DropIndex(
                name: "IX_code_nodes_workspace_path",
                table: "code_nodes");

            migrationBuilder.DropIndex(
                name: "IX_code_nodes_workspace_path_node_type",
                table: "code_nodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_code_edges",
                table: "code_edges");

            migrationBuilder.DropIndex(
                name: "IX_code_edges_source_id_edge_type",
                table: "code_edges");

            migrationBuilder.DropIndex(
                name: "IX_code_edges_target_id_edge_type",
                table: "code_edges");

            migrationBuilder.DropPrimaryKey(
                name: "PK_agent_executions",
                table: "agent_executions");

            migrationBuilder.DropIndex(
                name: "IX_agent_executions_conversation_id",
                table: "agent_executions");

            migrationBuilder.DropIndex(
                name: "IX_agent_executions_success",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "model",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "tokens_used",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "agent_id",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "workspace_path",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "modifiers",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "signature",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "workspace_path",
                table: "code_nodes");

            migrationBuilder.DropColumn(
                name: "conversation_id",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "model",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "prompt",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "agent_executions");

            migrationBuilder.DropColumn(
                name: "success",
                table: "agent_executions");

            migrationBuilder.RenameIndex(
                name: "IX_messages_conversation_id",
                table: "messages",
                newName: "ix_messages_conversation_id");

            migrationBuilder.RenameIndex(
                name: "IX_conversations_created_at",
                table: "conversations",
                newName: "ix_conversations_created_at");

            migrationBuilder.RenameColumn(
                name: "indexed_at",
                table: "code_nodes",
                newName: "updated_at");

            migrationBuilder.RenameIndex(
                name: "IX_code_nodes_node_type",
                table: "code_nodes",
                newName: "ix_code_nodes_node_type");

            migrationBuilder.RenameIndex(
                name: "IX_code_nodes_name",
                table: "code_nodes",
                newName: "ix_code_nodes_name");

            migrationBuilder.RenameColumn(
                name: "target_id",
                table: "code_edges",
                newName: "target_node_id");

            migrationBuilder.RenameColumn(
                name: "source_id",
                table: "code_edges",
                newName: "source_node_id");

            migrationBuilder.RenameColumn(
                name: "edge_type",
                table: "code_edges",
                newName: "relationship_type");

            migrationBuilder.RenameIndex(
                name: "IX_code_edges_target_id",
                table: "code_edges",
                newName: "ix_code_edges_target_node_id");

            migrationBuilder.RenameIndex(
                name: "IX_code_edges_source_id",
                table: "code_edges",
                newName: "ix_code_edges_source_node_id");

            migrationBuilder.RenameIndex(
                name: "IX_code_edges_edge_type",
                table: "code_edges",
                newName: "ix_code_edges_relationship_type");

            migrationBuilder.RenameColumn(
                name: "response",
                table: "agent_executions",
                newName: "error");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "agent_executions",
                newName: "ended_at");

            migrationBuilder.RenameIndex(
                name: "IX_agent_executions_started_at",
                table: "agent_executions",
                newName: "ix_agent_executions_started_at");

            migrationBuilder.RenameIndex(
                name: "IX_agent_executions_agent_id",
                table: "agent_executions",
                newName: "ix_agent_executions_agent_id");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "conversations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "metadata",
                table: "conversations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "code_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "code_nodes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "code_nodes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "fully_qualified_name",
                table: "code_nodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_code",
                table: "code_nodes",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "code_edges",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at",
                table: "code_edges",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "duration",
                table: "agent_executions",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "input",
                table: "agent_executions",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "output",
                table: "agent_executions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "agent_executions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "pk_messages",
                table: "messages",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_conversations",
                table: "conversations",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_code_nodes",
                table: "code_nodes",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_code_edges",
                table: "code_edges",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_agent_executions",
                table: "agent_executions",
                column: "id");

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_document_chunks_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_code_nodes_fully_qualified_name",
                table: "code_nodes",
                column: "fully_qualified_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_chunks_document_id",
                table: "document_chunks",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_source",
                table: "documents",
                column: "source",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_code_edges_code_nodes_source_node_id",
                table: "code_edges",
                column: "source_node_id",
                principalTable: "code_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_code_edges_code_nodes_target_node_id",
                table: "code_edges",
                column: "target_node_id",
                principalTable: "code_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_messages_conversations_conversation_id",
                table: "messages",
                column: "conversation_id",
                principalTable: "conversations",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
