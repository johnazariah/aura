# Research Workflows & Academic Features

**Status:** 📋 Backlog  
**Priority:** Medium  
**Source:** Gap Analysis vs Birdlet/Agent Orchestrator  
**Estimated Effort:** 4-5 weeks

## Overview

Add research-oriented workflows to Aura: paper discovery, literature review automation, citation management, and academic project patterns. This enables researchers, PhD students, and R&D teams to use Aura for knowledge work beyond pure software development.

## Strategic Context

Birdlet was purpose-built for academic research workflows. While Aura excels at code development, it lacks features for:
- Discovering relevant academic papers
- Synthesizing literature into summaries
- Managing citations and bibliographies
- Research-to-code pipelines

Adding these capabilities would expand Aura's addressable market to:
- PhD students and academic researchers
- R&D teams in industry
- Technical writers and documentation teams
- Anyone doing knowledge-intensive work

## Use Cases

1. **Literature Discovery** — "Find papers about transformer attention mechanisms published after 2022"
2. **Paper Summarization** — "Summarize this PDF and extract the key findings"
3. **Comparative Analysis** — "Compare these three papers' approaches to the problem"
4. **Citation Generation** — "Generate a BibTeX entry for this paper"
5. **Research Synthesis** — "Synthesize the literature I've ingested into a literature review section"
6. **Research-to-Code** — "Implement the algorithm described in section 3.2 of this paper"

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Research Workflow System                      │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────────┐
        │                     │                         │
        ▼                     ▼                         ▼
┌───────────────┐    ┌───────────────┐    ┌─────────────────────┐
│ Paper         │    │ Literature    │    │ Citation            │
│ Discovery     │    │ Analysis      │    │ Management          │
│               │    │               │    │                     │
│ - Semantic    │    │ - Summarize   │    │ - BibTeX export     │
│   Scholar API │    │ - Compare     │    │ - Reference parsing │
│ - arXiv API   │    │ - Synthesize  │    │ - Format conversion │
│ - CrossRef    │    │ - Q&A         │    │ - Deduplication     │
└───────────────┘    └───────────────┘    └─────────────────────┘
        │                     │                         │
        └─────────────────────┼─────────────────────────┘
                              │
                              ▼
                    ┌───────────────────┐
                    │ Research RAG      │
                    │ (Papers +         │
                    │  Documents)       │
                    └───────────────────┘
```

## Implementation

### Phase 1: Paper Discovery Service (Week 1-2)

**New Service:** `Aura.Foundation.Research.PaperDiscoveryService`

```csharp
namespace Aura.Foundation.Research;

public interface IPaperDiscoveryService
{
    Task<IReadOnlyList<Paper>> SearchAsync(PaperSearchRequest request, CancellationToken ct = default);
    Task<Paper> GetDetailsAsync(string paperId, PaperSource source, CancellationToken ct = default);
    Task<string> DownloadPdfAsync(string paperId, PaperSource source, string outputPath, CancellationToken ct = default);
}

public record PaperSearchRequest(
    string Query,
    int Limit = 20,
    int? YearFrom = null,
    int? YearTo = null,
    string[]? Fields = null,      // e.g., ["computer science", "machine learning"]
    PaperSource[]? Sources = null  // Default: all sources
);

public record Paper(
    string Id,
    string Title,
    string Abstract,
    string[] Authors,
    int Year,
    string? Venue,                 // Conference/journal name
    string? Doi,
    string? ArxivId,
    string? PdfUrl,
    int? CitationCount,
    PaperSource Source
);

public enum PaperSource
{
    SemanticScholar,
    ArXiv,
    CrossRef
}
```

**Semantic Scholar Integration:**

```csharp
public class SemanticScholarClient : IPaperSource
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.semanticscholar.org/graph/v1";
    
    public async Task<IReadOnlyList<Paper>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var url = $"{BaseUrl}/paper/search?query={Uri.EscapeDataString(query)}&limit={limit}" +
                  "&fields=paperId,title,abstract,authors,year,venue,citationCount,externalIds,openAccessPdf";
        
        var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadFromJsonAsync<SemanticScholarResponse>(ct);
        
        return json.Data.Select(p => new Paper(
            Id: p.PaperId,
            Title: p.Title,
            Abstract: p.Abstract ?? "",
            Authors: p.Authors.Select(a => a.Name).ToArray(),
            Year: p.Year ?? 0,
            Venue: p.Venue,
            Doi: p.ExternalIds?.Doi,
            ArxivId: p.ExternalIds?.ArXiv,
            PdfUrl: p.OpenAccessPdf?.Url,
            CitationCount: p.CitationCount,
            Source: PaperSource.SemanticScholar
        )).ToList();
    }
}
```

**arXiv Integration:**

```csharp
public class ArXivClient : IPaperSource
{
    private const string BaseUrl = "http://export.arxiv.org/api/query";
    
    public async Task<IReadOnlyList<Paper>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var url = $"{BaseUrl}?search_query=all:{Uri.EscapeDataString(query)}&max_results={limit}";
        
        var response = await _http.GetAsync(url, ct);
        var xml = await response.Content.ReadAsStringAsync(ct);
        
        // Parse Atom XML feed
        var doc = XDocument.Parse(xml);
        var ns = XNamespace.Get("http://www.w3.org/2005/Atom");
        
        return doc.Descendants(ns + "entry").Select(entry => new Paper(
            Id: ExtractArxivId(entry.Element(ns + "id")?.Value ?? ""),
            Title: entry.Element(ns + "title")?.Value?.Trim() ?? "",
            Abstract: entry.Element(ns + "summary")?.Value?.Trim() ?? "",
            Authors: entry.Elements(ns + "author").Select(a => a.Element(ns + "name")?.Value ?? "").ToArray(),
            Year: DateTime.Parse(entry.Element(ns + "published")?.Value ?? "").Year,
            Venue: "arXiv",
            Doi: null,
            ArxivId: ExtractArxivId(entry.Element(ns + "id")?.Value ?? ""),
            PdfUrl: entry.Elements(ns + "link").FirstOrDefault(l => l.Attribute("title")?.Value == "pdf")?.Attribute("href")?.Value,
            CitationCount: null,
            Source: PaperSource.ArXiv
        )).ToList();
    }
}
```

### Phase 2: Research Agent (Week 2-3)

**New Agent Definition:** `agents/research-agent.md`

```markdown
# Research Agent

You are a research assistant specialized in academic literature analysis. You help users discover papers, summarize findings, and synthesize knowledge.

## Capabilities

- Discover relevant academic papers using search APIs
- Summarize paper abstracts and full texts
- Compare and contrast multiple papers
- Extract key findings, methods, and contributions
- Generate literature review sections
- Create citation entries in various formats

## Tools

### discover_papers
Search for academic papers on a topic.

**Parameters:**
- query (string, required): Search query
- limit (int, optional): Max results (default: 10)
- year_from (int, optional): Minimum publication year
- sources (string[], optional): Which APIs to search

### summarize_paper
Summarize a paper from the knowledge base.

**Parameters:**
- paper_id (string, required): ID of the ingested paper
- style (string, optional): "brief" | "detailed" | "key-findings"

### compare_papers
Compare multiple papers on the same topic.

**Parameters:**
- paper_ids (string[], required): IDs of papers to compare
- aspects (string[], optional): Aspects to compare (methods, results, limitations)

### generate_citation
Generate a citation in the specified format.

**Parameters:**
- paper_id (string, required): Paper to cite
- format (string, required): "bibtex" | "apa" | "mla" | "ieee" | "chicago"

### synthesize_literature
Create a literature review from ingested papers.

**Parameters:**
- topic (string, required): Topic focus
- paper_ids (string[], optional): Specific papers to include
- style (string, optional): "narrative" | "thematic" | "chronological"

## Behavior

1. When asked to find papers, use discover_papers and present results clearly
2. When summarizing, focus on contributions, methods, and key findings
3. When comparing, highlight similarities and differences objectively
4. When synthesizing, organize by themes and cite sources properly
5. Always maintain academic rigor and accuracy
```

**Tool Implementations:**

```csharp
public class DiscoverPapersTool : ITool
{
    public string Name => "discover_papers";
    public string Description => "Search for academic papers on a topic";
    
    private readonly IPaperDiscoveryService _discovery;
    
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        var query = parameters.GetProperty("query").GetString()!;
        var limit = parameters.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;
        var yearFrom = parameters.TryGetProperty("year_from", out var y) ? y.GetInt32() : (int?)null;
        
        var papers = await _discovery.SearchAsync(new PaperSearchRequest(
            Query: query,
            Limit: limit,
            YearFrom: yearFrom
        ), ct);
        
        var result = papers.Select(p => new {
            p.Id,
            p.Title,
            p.Authors,
            p.Year,
            p.Venue,
            p.CitationCount,
            AbstractPreview = p.Abstract.Length > 300 ? p.Abstract[..300] + "..." : p.Abstract
        });
        
        return ToolResult.Success(JsonSerializer.Serialize(result));
    }
}

public class GenerateCitationTool : ITool
{
    public string Name => "generate_citation";
    
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        var paperId = parameters.GetProperty("paper_id").GetString()!;
        var format = parameters.GetProperty("format").GetString()!;
        
        var paper = await _ragService.GetPaperMetadataAsync(paperId, ct);
        if (paper == null)
            return ToolResult.Error($"Paper {paperId} not found in knowledge base");
        
        var citation = format.ToLower() switch
        {
            "bibtex" => FormatBibTeX(paper),
            "apa" => FormatAPA(paper),
            "mla" => FormatMLA(paper),
            "ieee" => FormatIEEE(paper),
            _ => throw new ArgumentException($"Unknown format: {format}")
        };
        
        return ToolResult.Success(citation);
    }
    
    private string FormatBibTeX(PaperMetadata p)
    {
        var key = $"{p.Authors.FirstOrDefault()?.Split(' ').Last() ?? "unknown"}{p.Year}";
        return $"""
            @article{{{key},
                title = {{{p.Title}}},
                author = {{{string.Join(" and ", p.Authors)}}},
                year = {{{p.Year}}},
                journal = {{{p.Venue ?? "Unknown"}}},
                doi = {{{p.Doi ?? ""}}}
            }}
            """;
    }
    
    private string FormatAPA(PaperMetadata p)
    {
        var authors = string.Join(", ", p.Authors.Select(FormatAPAAuthor));
        return $"{authors} ({p.Year}). {p.Title}. {p.Venue ?? ""}. https://doi.org/{p.Doi}";
    }
}
```

### Phase 3: Literature Synthesis (Week 3-4)

**Synthesis Pattern:** `patterns/literature-review.md`

```markdown
# Literature Review Generation

## Objective
Synthesize ingested papers into a coherent literature review section.

## Inputs
- topic: The research topic/question
- paper_ids: Optional list of specific papers
- style: "narrative" | "thematic" | "chronological"

## Steps

### 1. Gather Context
Retrieve all relevant papers from the knowledge base:
- Use RAG search with the topic query
- Filter to papers only (content_type = 'document-pdf')
- Gather full abstracts and key sections

### 2. Identify Themes
Analyze the papers to identify:
- Major themes and approaches
- Points of agreement and disagreement
- Evolution of ideas over time
- Gaps in the literature

### 3. Organize Structure
Based on style:
- **Thematic**: Group by common themes/approaches
- **Chronological**: Order by publication date
- **Narrative**: Tell the story of how ideas developed

### 4. Generate Review
Write the literature review:
- Introduction: scope and importance
- Body: organized discussion of papers
- Synthesis: what the literature collectively shows
- Gaps: what remains unexplored

### 5. Generate Citations
Create properly formatted citations for all referenced papers.

## Output Format
```markdown
## Literature Review: {topic}

### Introduction
{context and importance}

### {Theme 1}
{discussion with citations}

### {Theme 2}
{discussion with citations}

### Synthesis
{what the literature collectively tells us}

### Research Gaps
{opportunities for future work}

## References
{formatted bibliography}
```
```

### Phase 4: Research Workflow Integration (Week 4-5)

**Research Workflow Type:**

```csharp
public class ResearchWorkflowType : IWorkflowType
{
    public string Name => "research";
    public string Description => "Literature review and research synthesis workflow";
    
    public IReadOnlyList<WorkflowStep> GetDefaultSteps() => new[]
    {
        new WorkflowStep("discover", "Discover relevant papers", "research-agent"),
        new WorkflowStep("ingest", "Download and ingest papers", "research-agent"),
        new WorkflowStep("analyze", "Analyze and summarize papers", "research-agent"),
        new WorkflowStep("synthesize", "Synthesize into literature review", "research-agent"),
        new WorkflowStep("export", "Export bibliography and review", "research-agent")
    };
}
```

**API Endpoints:**

```csharp
// Paper discovery
app.MapGet("/api/research/papers/search", async (
    [FromQuery] string query,
    [FromQuery] int limit,
    [FromQuery] int? yearFrom,
    IPaperDiscoveryService discovery,
    CancellationToken ct) =>
{
    var papers = await discovery.SearchAsync(new PaperSearchRequest(query, limit, yearFrom), ct);
    return Results.Ok(papers);
})
.WithName("SearchPapers")
.WithTags("Research");

// Paper ingestion (download + index)
app.MapPost("/api/research/papers/ingest", async (
    IngestPaperRequest request,
    IPaperDiscoveryService discovery,
    IIndexingService indexing,
    CancellationToken ct) =>
{
    // Download PDF
    var pdfPath = await discovery.DownloadPdfAsync(request.PaperId, request.Source, request.OutputDir, ct);
    
    // Ingest into RAG
    await indexing.IndexFileAsync(pdfPath, request.WorkspaceId, ct);
    
    return Results.Ok(new { path = pdfPath, message = "Paper ingested successfully" });
})
.WithName("IngestPaper")
.WithTags("Research");

// Citation export
app.MapGet("/api/research/citations/export", async (
    [FromQuery] Guid workspaceId,
    [FromQuery] string format,
    ICitationService citations,
    CancellationToken ct) =>
{
    var bibliography = await citations.ExportBibliographyAsync(workspaceId, format, ct);
    return Results.Text(bibliography, format == "bibtex" ? "application/x-bibtex" : "text/plain");
})
.WithName("ExportCitations")
.WithTags("Research");
```

**VS Code Commands:**

```typescript
// extension/src/commands/research.ts
export function registerResearchCommands(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.commands.registerCommand('aura.searchPapers', async () => {
            const query = await vscode.window.showInputBox({
                prompt: 'Search for papers',
                placeHolder: 'e.g., "transformer attention mechanisms"'
            });
            
            if (query) {
                const papers = await auraClient.get(`/api/research/papers/search?query=${encodeURIComponent(query)}&limit=20`);
                showPaperResultsPanel(papers);
            }
        }),
        
        vscode.commands.registerCommand('aura.createResearchWorkflow', async () => {
            const topic = await vscode.window.showInputBox({
                prompt: 'Research topic',
                placeHolder: 'e.g., "Large language model fine-tuning techniques"'
            });
            
            if (topic) {
                await auraClient.post('/api/developer/workflows', {
                    title: `Research: ${topic}`,
                    description: `Literature review on: ${topic}`,
                    type: 'research'
                });
            }
        }),
        
        vscode.commands.registerCommand('aura.exportBibliography', async () => {
            const format = await vscode.window.showQuickPick(
                ['BibTeX', 'APA', 'IEEE', 'MLA'],
                { placeHolder: 'Select citation format' }
            );
            
            if (format) {
                const workspaceId = await getActiveWorkspaceId();
                const bib = await auraClient.get(`/api/research/citations/export?workspaceId=${workspaceId}&format=${format.toLowerCase()}`);
                
                const doc = await vscode.workspace.openTextDocument({ content: bib, language: format === 'BibTeX' ? 'bibtex' : 'plaintext' });
                await vscode.window.showTextDocument(doc);
            }
        })
    );
}
```

## Database Changes

```sql
-- Paper metadata for citation management
CREATE TABLE papers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workspace_id UUID REFERENCES workspaces(id) ON DELETE CASCADE,
    external_id VARCHAR(255),           -- Semantic Scholar ID, arXiv ID, etc.
    source VARCHAR(50) NOT NULL,        -- 'semantic_scholar', 'arxiv', 'crossref', 'manual'
    title TEXT NOT NULL,
    authors TEXT[] NOT NULL,
    year INTEGER,
    venue TEXT,
    doi VARCHAR(255),
    arxiv_id VARCHAR(50),
    pdf_path TEXT,                      -- Local path after download
    abstract TEXT,
    citation_count INTEGER,
    ingested_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'
);

CREATE INDEX idx_papers_workspace ON papers(workspace_id);
CREATE INDEX idx_papers_external_id ON papers(source, external_id);

-- Link papers to RAG chunks
ALTER TABLE rag_chunks ADD COLUMN paper_id UUID REFERENCES papers(id);
```

## Configuration

```json
{
    "Research": {
        "Enabled": true,
        "SemanticScholar": {
            "ApiKey": "",              // Optional, for higher rate limits
            "RateLimit": 100           // Requests per 5 minutes
        },
        "ArXiv": {
            "Enabled": true
        },
        "DefaultCitationFormat": "bibtex",
        "PaperStoragePath": "./papers",
        "AutoIngestOnDiscover": false
    }
}
```

## Success Criteria

- [ ] Paper search returns results from Semantic Scholar and arXiv
- [ ] Papers can be downloaded and ingested into RAG
- [ ] Research agent can summarize and compare papers
- [ ] Citations can be exported in BibTeX, APA, IEEE, MLA formats
- [ ] Literature review synthesis works for 5+ papers
- [ ] VS Code commands for paper search and bibliography export
- [ ] Research workflow type available in workflow creation

## Testing

```csharp
[Fact]
public async Task SemanticScholarClient_SearchReturnsResults()
{
    var client = new SemanticScholarClient(new HttpClient());
    var results = await client.SearchAsync("attention is all you need", 5, CancellationToken.None);
    
    results.Should().NotBeEmpty();
    results.Should().Contain(p => p.Title.Contains("Attention", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public async Task GenerateCitationTool_CreatesBibTeX()
{
    var paper = new PaperMetadata("Test Paper", ["John Doe", "Jane Smith"], 2023, "NeurIPS", "10.1234/test");
    var tool = new GenerateCitationTool(_mockRag.Object);
    
    var result = await tool.ExecuteAsync(JsonDocument.Parse("""{"paper_id":"123","format":"bibtex"}""").RootElement, default);
    
    result.Content.Should().Contain("@article{");
    result.Content.Should().Contain("Doe2023");
}
```

## Future Enhancements

1. **Reference Graph** — Visualize citation relationships between papers
2. **Reading Lists** — Curated collections of papers by topic
3. **Annotation Sync** — Import highlights/notes from PDF readers
4. **Collaboration** — Share bibliographies with team members
5. **Research-to-Code** — "Implement algorithm from Section 3" pattern
6. **Zotero/Mendeley Integration** — Import from existing reference managers
