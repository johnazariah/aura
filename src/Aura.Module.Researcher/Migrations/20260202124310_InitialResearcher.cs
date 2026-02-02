using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Aura.Module.Researcher.Migrations
{
    /// <inheritdoc />
    public partial class InitialResearcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Definition = table.Column<string>(type: "text", nullable: true),
                    Aliases = table.Column<string[]>(type: "text[]", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Authors = table.Column<string[]>(type: "text[]", nullable: false),
                    Abstract = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Doi = table.Column<string>(type: "text", nullable: true),
                    ArxivId = table.Column<string>(type: "text", nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Venue = table.Column<string>(type: "text", nullable: true),
                    CitationCount = table.Column<int>(type: "integer", nullable: true),
                    PdfPath = table.Column<string>(type: "text", nullable: true),
                    MarkdownPath = table.Column<string>(type: "text", nullable: true),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ReadingStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Syntheses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Style = table.Column<int>(type: "integer", nullable: false),
                    Focus = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    SourceIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Syntheses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConceptLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    Relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Confidence = table.Column<float>(type: "real", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptLinks_Concepts_FromConceptId",
                        column: x => x.FromConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConceptLinks_Concepts_ToConceptId",
                        column: x => x.ToConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConceptLinks_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Excerpts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Annotation = table.Column<string>(type: "text", nullable: true),
                    Embedding = table.Column<Vector>(type: "vector(384)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Excerpts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Excerpts_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceConcepts",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConceptId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionCount = table.Column<int>(type: "integer", nullable: false),
                    IsPrimaryTopic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceConcepts", x => new { x.SourceId, x.ConceptId });
                    table.ForeignKey(
                        name: "FK_SourceConcepts_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SourceConcepts_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptLinks_FromConceptId",
                table: "ConceptLinks",
                column: "FromConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptLinks_SourceId",
                table: "ConceptLinks",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptLinks_ToConceptId",
                table: "ConceptLinks",
                column: "ToConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_Name",
                table: "Concepts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Excerpts_SourceId",
                table: "Excerpts",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceConcepts_ConceptId",
                table: "SourceConcepts",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_ArxivId",
                table: "Sources",
                column: "ArxivId");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_Doi",
                table: "Sources",
                column: "Doi");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_ReadingStatus",
                table: "Sources",
                column: "ReadingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Sources_SourceType",
                table: "Sources",
                column: "SourceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConceptLinks");

            migrationBuilder.DropTable(
                name: "Excerpts");

            migrationBuilder.DropTable(
                name: "SourceConcepts");

            migrationBuilder.DropTable(
                name: "Syntheses");

            migrationBuilder.DropTable(
                name: "Concepts");

            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
