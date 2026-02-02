
---

## Reference: Emic PDF Extraction Approach

The emic project uses a simpler but effective approach for PDF-to-Markdown conversion that we should consider:

### Tool: `pdftotext` (poppler-utils)

```bash
# Extract with layout preservation
pdftotext -layout "<PDF_PATH>" "<OUTPUT_TXT_PATH>"
```

**Advantages**:
- Fast, reliable, no dependencies beyond poppler
- Layout preservation handles multi-column papers well
- Works offline, no LLM costs for basic extraction

### Output Naming Convention

```
<PARENT_DIR>/
├── original.pdf
└── original/
    └── Title_Author_arXivID.md    # Descriptive filename
```

**Filename format**: `Title_Author(s)_Identifier.md`
- Examples:
  - `Attention_Is_All_You_Need_Vaswani_1706.03762.md`
  - `BERT_Devlin_1810.04805.md`

### Markdown Structure

```markdown
# [Paper Title]

**Authors:** [Author 1], [Author 2], ...
**Affiliation:** [Institution(s)]
**Source:** [arXiv ID / DOI]
**Date:** [Publication date]

---

## Abstract

[Abstract text]

---

## 1. Introduction

[Content...]

---

## References

[1] Author et al. Title. Venue, Year.
```

### Formatting Guidelines

| Element | Markdown Format |
|---------|-----------------|
| Main title | `# Title` |
| Section headings | `## Section` |
| Subsections | `### Subsection` |
| Inline math | `$equation$` |
| Display math | `$$equation$$` |
| Figure references | `[Figure N: Caption — see original PDF]` |
| Tables | Markdown tables or `[Table N: Caption — see original PDF]` |

### Hybrid Approach for Aura

We can combine both approaches:

1. **Phase 1: pdftotext extraction** (fast, cheap)
   - Use poppler's `pdftotext -layout` for raw extraction
   - Apply regex/heuristics for structure detection
   - Good enough for indexing and basic RAG

2. **Phase 2: LLM enhancement** (optional, higher quality)
   - Use LLM to clean up formatting
   - Better section detection
   - LaTeX equation cleanup
   - This could be a background job or on-demand

3. **Phase 3: Vision LLM** (optional, for figures)
   - Extract and describe figures
   - Only for papers where figures are important

### Implementation Consideration

```csharp
public interface IPdfExtractor
{
    /// <summary>
    /// Fast extraction using pdftotext
    /// </summary>
    Task<RawPdfContent> ExtractRawAsync(string pdfPath);
}

public interface IPdfToMarkdownService
{
    /// <summary>
    /// Full conversion with structure detection
    /// </summary>
    Task<MarkdownDocument> ConvertAsync(
        string pdfPath, 
        PdfConversionOptions? options = null);
    
    /// <summary>
    /// Enhance existing markdown with LLM
    /// </summary>
    Task<MarkdownDocument> EnhanceAsync(
        MarkdownDocument doc,
        EnhancementLevel level = EnhancementLevel.Basic);
}

public enum EnhancementLevel
{
    None,           // Raw pdftotext output
    Basic,          // Structure detection + cleanup
    Full,           // LLM-enhanced formatting
    WithFigures     // Full + Vision LLM for figures
}
```

This tiered approach lets us:
- Index papers quickly without LLM costs
- Enhance on-demand when user wants to read/study a paper
- Support offline operation (Phase 1 only)
