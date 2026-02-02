# Researcher Module

**Status:** ✅ Complete
**Completed:** 2026-02-02

## Overview

The Researcher module enables Aura to manage academic papers, articles, and web sources for research workflows. It provides paper import from arXiv and Semantic Scholar, local PDF storage, excerpt extraction, and concept mapping.

## Components

### Entities

| Entity | Purpose |
|--------|---------|
| `Source` | Base entity for papers, articles, web pages |
| `Excerpt` | Highlighted passages with page references |
| `Concept` | Research concepts/themes extracted from sources |
| `ConceptLink` | Relationships between concepts |
| `SourceConcept` | Many-to-many link between sources and concepts |
| `Synthesis` | AI-generated summaries across multiple sources |
| `SourceType` | Enum: ArxivPaper, SemanticScholarPaper, WebPage, ManualUpload |
| `ReadingStatus` | Enum: Unread, InProgress, Completed, Archived |

### Services

| Service | Purpose |
|---------|---------|
| `LibraryService` | CRUD operations for sources, excerpts, concepts |
| `SourceFetcherService` | Orchestrates fetchers by URL pattern |
| `ArxivFetcher` | Imports papers from arXiv API |
| `SemanticScholarFetcher` | Imports papers from Semantic Scholar API |
| `WebPageFetcher` | Extracts content from web pages |
| `PdfExtractor` | Extracts text from PDF files |
| `PdfToMarkdownService` | Converts PDFs to Markdown (future: Marker/Docling) |

### Database

- `ResearcherDbContext` with InMemory-compatible configuration for testing
- `InitialResearcher` migration for PostgreSQL with pgvector

### API Endpoints

All in `ResearcherEndpoints.cs`:

```
GET    /api/researcher/sources              # List all sources
GET    /api/researcher/sources/{id}         # Get source details
POST   /api/researcher/sources              # Create source
DELETE /api/researcher/sources/{id}         # Delete source
POST   /api/researcher/import               # Import from URL (arXiv, S2, web)
GET    /api/researcher/sources/{id}/excerpts # List excerpts
POST   /api/researcher/sources/{id}/excerpts # Add excerpt
GET    /api/researcher/concepts             # List concepts
POST   /api/researcher/search               # Search sources
```

### VS Code Extension

- **Research Library view** (`aura.research`) in sidebar
- Commands: `refreshResearch`, `importPaper`, `searchResearch`, `openSource`, `deleteSource`, `convertToMarkdown`
- Tree provider showing sources grouped by reading status

### Agents

| Agent | Purpose |
|-------|---------|
| `research-agent.md` | Assists with literature review and synthesis |
| `reading-assistant-agent.md` | Helps extract key points from papers |

## Testing

29 unit tests covering:
- `LibraryServiceTests` - 12 tests for CRUD operations
- `ArxivFetcherTests` - 4 tests for arXiv API parsing
- `SourceFetcherServiceTests` - 6 tests for URL routing and fallback

InMemory database support required conditional model configuration in `ResearcherDbContext.OnModelCreating` to handle pgvector types.

## Future Enhancements

- Semantic Scholar API key configuration for higher rate limits
- PDF-to-Markdown integration with Marker or Docling
- LLM-powered concept extraction
- Cross-source synthesis generation
- Citation graph visualization

## Design Documents

- [researcher-module.md](../design/researcher-module.md) - Original design spec
- [emic-pdf-approach.md](../design/emic-pdf-approach.md) - PDF extraction philosophy
