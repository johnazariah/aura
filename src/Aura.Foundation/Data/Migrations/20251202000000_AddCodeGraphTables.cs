using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aura.Foundation.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeGraphTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    workspace_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    properties = table.Column<string>(type: "jsonb", nullable: true),
                    embedding = table.Column<float[]>(type: "vector(768)", nullable: true),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_code_nodes", x => x.id);
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

            // Create indexes for code_nodes
            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_node_type",
                table: "code_nodes",
                column: "node_type");

            migrationBuilder.CreateIndex(
                name: "IX_code_nodes_name",
                table: "code_nodes",
                column: "name");

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

            // Create indexes for code_edges
            migrationBuilder.CreateIndex(
                name: "IX_code_edges_source_id",
                table: "code_edges",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_target_id",
                table: "code_edges",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_edge_type",
                table: "code_edges",
                column: "edge_type");

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_source_id_edge_type",
                table: "code_edges",
                columns: new[] { "source_id", "edge_type" });

            migrationBuilder.CreateIndex(
                name: "IX_code_edges_target_id_edge_type",
                table: "code_edges",
                columns: new[] { "target_id", "edge_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "code_edges");

            migrationBuilder.DropTable(
                name: "code_nodes");
        }
    }
}
