# Multi-Format Document Ingestion

**Status:** 📋 Backlog  
**Priority:** High  
**Source:** Gap Analysis vs Birdlet/Agent Orchestrator  
**Estimated Effort:** 3-4 weeks

## Overview

Extend Aura's RAG system to ingest non-code documents: PDFs, Office documents (Word, Excel, PowerPoint), Markdown, HTML, and plain text. Currently Aura only indexes source code files, limiting its usefulness for projects with significant documentation, design specs, or research papers.

## Strategic Context

The Birdlet platform has comprehensive document ingestion capabilities that Aura lacks. Adding this would:
- Enable knowledge-enhanced agents to reference design docs, specs, and requirements
- Support research-oriented workflows with paper/literature ingestion
- Make Aura useful for documentation-heavy enterprise projects
- Bridge the gap between code understanding and project knowledge

## Use Cases

1. **Design Documents** — Index architecture docs, ADRs, design specs so agents understand "why" not just "what"
2. **API Documentation** — Ingest OpenAPI specs, README files, API guides
3. **Research Papers** — PhD students/researchers can index papers and ask questions
4. **Meeting Notes** — Index meeting notes, decisions, context that informed the code
5. **Requirements** — PRDs, user stories, acceptance criteria as agent context

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Document Ingestion Pipeline                   │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│ PDF Ingester  │    │ Office        │    │ Markdown      │
│               │    │ Ingester      │    │ Ingester      │
│ - PdfPig      │    │ - OpenXML SDK │    │ - Markdig     │
│ - OCR option  │    │ - ExcelDataRdr│    │ - Front matter│
└───────┬───────┘    └───────┬───────┘    └───────┬───────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                              ▼
                    ┌───────────────────┐
                    │ Unified Chunking  │
                    │ Service           │
                    │ - Section-aware   │
                    │ - Semantic breaks │
                    └─────────┬─────────┘
                              │
                              ▼
                    ┌───────────────────┐
                    │ Embedding +       │
                    │ RAG Storage       │
                    │ (existing)        │
                    └───────────────────┘
```

## Document Type Support

| Format | Library | Features | Priority |
|--------|---------|----------|----------|
| **PDF** | PdfPig | Text extraction, layout detection | High |
| **Word (.docx)** | DocumentFormat.OpenXml | Paragraphs, headings, tables | High |
| **Markdown (.md)** | Markdig | Headers, code blocks, front matter | High |
| **HTML** | HtmlAgilityPack | Article extraction, cleanup | Medium |
| **Excel (.xlsx)** | ExcelDataReader | Tables as structured data | Medium |
| **PowerPoint (.pptx)** | OpenXml | Slide text, speaker notes | Low |
| **Plain Text (.txt)** | Native | Simple chunking | High |

## Implementation

### Phase 1: Core Infrastructure (Week 1)

**New Project:** `Aura.Foundation.Documents`

```csharp
namespace Aura.Foundation.Documents;

public interface IDocumentIngester
{
    string[] SupportedExtensions { get; }
    Task<DocumentContent> ExtractAsync(string filePath, CancellationToken ct = default);
}

public record DocumentContent(
    string Title,
    string FullText,
    IReadOnlyList<DocumentSection> Sections,
    DocumentMetadata Metadata
);

public record DocumentSection(
    string Heading,
    string Content,
    int Level,           // h1=1, h2=2, etc.
    int StartOffset,
    int EndOffset
);

public record DocumentMetadata(
    string Author,
    DateTime? CreatedDate,
    DateTime? ModifiedDate,
    Dictionary<string, string> CustomProperties
);
```

**Chunking Service:**

```csharp
public class DocumentChunkingService : IDocumentChunkingService
{
    private readonly ChunkingOptions _options;
    
    public IReadOnlyList<RagChunk> ChunkDocument(DocumentContent doc, string filePath)
    {
        var chunks = new List<RagChunk>();
        
        // Strategy 1: Section-based chunking (preferred)
        if (doc.Sections.Count > 0)
        {
            foreach (var section in doc.Sections)
            {
                // Split large sections into overlapping chunks
                chunks.AddRange(ChunkSection(section, filePath, doc.Title));
            }
        }
        else
        {
            // Strategy 2: Sliding window for unstructured text
            chunks.AddRange(SlidingWindowChunk(doc.FullText, filePath, doc.Title));
        }
        
        return chunks;
    }
    
    private IEnumerable<RagChunk> ChunkSection(DocumentSection section, string path, string title)
    {
        const int maxChunkSize = 1500;  // tokens
        const int overlap = 200;
        
        if (section.Content.Length <= maxChunkSize)
        {
            yield return new RagChunk
            {
                Content = $"# {section.Heading}\n\n{section.Content}",
                FilePath = path,
                NodeType = "document-section",
                Metadata = new { title, heading = section.Heading, level = section.Level }
            };
        }
        else
        {
            // Split with overlap, preserving heading context
            foreach (var chunk in SplitWithOverlap(section.Content, maxChunkSize, overlap))
            {
                yield return new RagChunk
                {
                    Content = $"# {section.Heading}\n\n{chunk}",
                    FilePath = path,
                    NodeType = "document-section",
                    Metadata = new { title, heading = section.Heading, level = section.Level }
                };
            }
        }
    }
}
```

### Phase 2: Format-Specific Ingesters (Week 2)

**PDF Ingester:**

```csharp
public class PdfIngester : IDocumentIngester
{
    public string[] SupportedExtensions => [".pdf"];
    
    public async Task<DocumentContent> ExtractAsync(string filePath, CancellationToken ct)
    {
        using var document = PdfDocument.Open(filePath);
        
        var sections = new List<DocumentSection>();
        var fullText = new StringBuilder();
        
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            fullText.AppendLine(text);
            
            // Detect headings by font size/bold (heuristic)
            var blocks = ContentOrderTextExtractor.GetText(page);
            // ... heading detection logic
        }
        
        return new DocumentContent(
            Title: Path.GetFileNameWithoutExtension(filePath),
            FullText: fullText.ToString(),
            Sections: sections,
            Metadata: ExtractPdfMetadata(document)
        );
    }
}
```

**Word Ingester:**

```csharp
public class WordIngester : IDocumentIngester
{
    public string[] SupportedExtensions => [".docx"];
    
    public async Task<DocumentContent> ExtractAsync(string filePath, CancellationToken ct)
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document.Body;
        
        var sections = new List<DocumentSection>();
        var currentSection = new StringBuilder();
        string currentHeading = "Introduction";
        int currentLevel = 1;
        
        foreach (var para in body.Elements<Paragraph>())
        {
            var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            
            if (style?.StartsWith("Heading") == true)
            {
                // Save previous section
                if (currentSection.Length > 0)
                {
                    sections.Add(new DocumentSection(currentHeading, currentSection.ToString(), currentLevel, 0, 0));
                    currentSection.Clear();
                }
                
                currentHeading = para.InnerText;
                currentLevel = int.Parse(style.Replace("Heading", ""));
            }
            else
            {
                currentSection.AppendLine(para.InnerText);
            }
        }
        
        // Don't forget last section
        if (currentSection.Length > 0)
        {
            sections.Add(new DocumentSection(currentHeading, currentSection.ToString(), currentLevel, 0, 0));
        }
        
        return new DocumentContent(
            Title: doc.PackageProperties.Title ?? Path.GetFileNameWithoutExtension(filePath),
            FullText: string.Join("\n", sections.Select(s => s.Content)),
            Sections: sections,
            Metadata: ExtractWordMetadata(doc)
        );
    }
}
```

**Markdown Ingester:**

```csharp
public class MarkdownIngester : IDocumentIngester
{
    public string[] SupportedExtensions => [".md", ".markdown"];
    
    public async Task<DocumentContent> ExtractAsync(string filePath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, ct);
        
        // Parse front matter (YAML header)
        var (frontMatter, body) = ParseFrontMatter(content);
        
        // Use Markdig to parse structure
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var doc = Markdown.Parse(body, pipeline);
        
        var sections = new List<DocumentSection>();
        var currentHeading = "Document";
        var currentContent = new StringBuilder();
        
        foreach (var block in doc)
        {
            if (block is HeadingBlock heading)
            {
                if (currentContent.Length > 0)
                {
                    sections.Add(new DocumentSection(currentHeading, currentContent.ToString(), heading.Level, 0, 0));
                    currentContent.Clear();
                }
                currentHeading = heading.Inline?.FirstChild?.ToString() ?? "Section";
            }
            else
            {
                currentContent.AppendLine(block.ToString());
            }
        }
        
        return new DocumentContent(
            Title: frontMatter.GetValueOrDefault("title") ?? Path.GetFileNameWithoutExtension(filePath),
            FullText: body,
            Sections: sections,
            Metadata: new DocumentMetadata(
                frontMatter.GetValueOrDefault("author"),
                null, null,
                frontMatter
            )
        );
    }
}
```

### Phase 3: Integration with Existing RAG (Week 3)

**Extend IndexingService:**

```csharp
public class IndexingService
{
    private readonly IEnumerable<IDocumentIngester> _documentIngesters;
    private readonly IDocumentChunkingService _chunkingService;
    
    public async Task IndexFileAsync(string filePath, Guid workspaceId, CancellationToken ct)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        // Try code ingesters first (existing behavior)
        var codeIngester = _codeIngesters.FirstOrDefault(i => i.SupportedExtensions.Contains(extension));
        if (codeIngester != null)
        {
            await IndexCodeFileAsync(filePath, workspaceId, codeIngester, ct);
            return;
        }
        
        // Try document ingesters (new)
        var docIngester = _documentIngesters.FirstOrDefault(i => i.SupportedExtensions.Contains(extension));
        if (docIngester != null)
        {
            await IndexDocumentAsync(filePath, workspaceId, docIngester, ct);
            return;
        }
        
        _logger.LogDebug("No ingester for {Extension}, skipping {File}", extension, filePath);
    }
    
    private async Task IndexDocumentAsync(
        string filePath, 
        Guid workspaceId, 
        IDocumentIngester ingester, 
        CancellationToken ct)
    {
        var doc = await ingester.ExtractAsync(filePath, ct);
        var chunks = _chunkingService.ChunkDocument(doc, filePath);
        
        foreach (var chunk in chunks)
        {
            chunk.WorkspaceId = workspaceId;
            chunk.Embedding = await _embeddingService.GenerateAsync(chunk.Content, ct);
            await _ragRepository.UpsertAsync(chunk, ct);
        }
        
        _logger.LogInformation("Indexed document {File}: {ChunkCount} chunks", filePath, chunks.Count);
    }
}
```

**API Endpoint:**

```csharp
// In Program.cs
app.MapPost("/api/workspaces/{id}/ingest-document", async (
    Guid id,
    IngestDocumentRequest request,
    IIndexingService indexing,
    CancellationToken ct) =>
{
    await indexing.IndexFileAsync(request.FilePath, id, ct);
    return Results.Ok(new { message = $"Ingested {request.FilePath}" });
})
.WithName("IngestDocument")
.WithTags("Indexing");
```

### Phase 4: VS Code Integration (Week 4)

**Context Menu Command:**

```typescript
// extension/src/commands/ingestDocument.ts
export async function ingestDocument(uri: vscode.Uri): Promise<void> {
    const workspaceId = await getActiveWorkspaceId();
    if (!workspaceId) {
        vscode.window.showErrorMessage('No workspace indexed. Index a folder first.');
        return;
    }
    
    const response = await auraClient.post(`/api/workspaces/${workspaceId}/ingest-document`, {
        filePath: uri.fsPath
    });
    
    vscode.window.showInformationMessage(`Ingested: ${path.basename(uri.fsPath)}`);
}

// Register in package.json
{
    "contributes": {
        "menus": {
            "explorer/context": [
                {
                    "command": "aura.ingestDocument",
                    "when": "resourceExtname =~ /\\.(pdf|docx|xlsx|md|txt|html)$/",
                    "group": "aura@1"
                }
            ]
        },
        "commands": [
            {
                "command": "aura.ingestDocument",
                "title": "Ingest Document into Aura",
                "category": "Aura"
            }
        ]
    }
}
```

## Database Changes

Add content_type to existing rag_chunks table:

```sql
ALTER TABLE rag_chunks ADD COLUMN content_type VARCHAR(50) DEFAULT 'code';
-- Values: 'code', 'document-pdf', 'document-word', 'document-markdown', etc.

CREATE INDEX idx_rag_chunks_content_type ON rag_chunks(content_type);
```

## Configuration

```json
{
    "Indexing": {
        "DocumentIngestion": {
            "Enabled": true,
            "SupportedExtensions": [".pdf", ".docx", ".md", ".txt", ".html"],
            "MaxFileSizeMb": 50,
            "ChunkSize": 1500,
            "ChunkOverlap": 200,
            "PdfOcrEnabled": false
        }
    }
}
```

## NuGet Dependencies

```xml
<PackageReference Include="PdfPig" Version="0.1.8" />
<PackageReference Include="DocumentFormat.OpenXml" Version="3.0.0" />
<PackageReference Include="ExcelDataReader" Version="3.6.0" />
<PackageReference Include="Markdig" Version="0.34.0" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.54" />
```

## Success Criteria

- [ ] PDF files can be ingested and chunked by section
- [ ] Word documents preserve heading structure
- [ ] Markdown files parse front matter and headers
- [ ] RAG search returns relevant document chunks
- [ ] VS Code context menu allows document ingestion
- [ ] Chunk metadata includes source document title and section
- [ ] Performance: 100-page PDF ingests in < 30 seconds

## Testing

```csharp
[Fact]
public async Task PdfIngester_ExtractsTextWithSections()
{
    var ingester = new PdfIngester();
    var result = await ingester.ExtractAsync("testdata/sample.pdf");
    
    result.FullText.Should().NotBeEmpty();
    result.Sections.Should().HaveCountGreaterThan(0);
}

[Fact]
public async Task WordIngester_ParsesHeadings()
{
    var ingester = new WordIngester();
    var result = await ingester.ExtractAsync("testdata/spec.docx");
    
    result.Sections.Should().Contain(s => s.Level == 1);
    result.Sections.Should().Contain(s => s.Level == 2);
}

[Fact]
public async Task DocumentChunking_PreservesHeadingContext()
{
    var doc = new DocumentContent("Test", "...", 
        [new DocumentSection("Introduction", "Long content...", 1, 0, 0)], 
        new DocumentMetadata(null, null, null, new()));
    
    var chunks = _chunkingService.ChunkDocument(doc, "test.pdf");
    
    chunks.Should().AllSatisfy(c => c.Content.Should().StartWith("# Introduction"));
}
```

## Future Enhancements

1. **OCR for scanned PDFs** — Tesseract integration for image-based PDFs
2. **Table extraction** — Structured data from tables as separate chunks
3. **Image captioning** — Extract/describe images in documents
4. **Incremental updates** — Re-index only changed pages
5. **Citation extraction** — Parse references for research workflows
