# Feature: SDD Artifact Export & Enhanced Research

**Status:** ✅ Complete  
**Completed:** 2026-02-06  
**Last Updated:** 2026-02-06  
**Created:** 2026-02-03  
**Author:** Architecture Review

## Problem Statement

Aura's workflow engine implements the same 4-phase pattern as SDD (Research → Plan → Implement → Validate), but there are gaps that reduce transparency and depth:

| Gap | Impact |
|-----|--------|
| **Artifacts in database, not files** | Can't version control workflow history alongside code |
| **Shallow research phase** | Codebase-only analysis misses best practices, prior art, risks |
| **No explicit open questions** | Backlog items don't drive research systematically |
| **No human-readable plan export** | Hard to review/share plans outside Aura UI |
| **No structured review checklist** | Validation is pass/fail, not nuanced |

## Goals

1. **Export SDD-compatible artifacts** from any workflow
2. **Deepen the research phase** with web search and risk analysis
3. **Support explicit open questions** that drive research
4. **Generate human-readable plan documents** alongside database records
5. **Add structured review checklists** to validation phase

## Non-Goals

- Replacing database storage with files (database remains primary)
- Full SDD agent integration (Aura keeps its own agents)
- Retroactive export of historical workflows (new workflows only)

---

## Feature 1: Artifact Export

### Summary

Add API endpoints to export workflow artifacts as markdown files compatible with SDD folder structure.

### API Design

```
POST /api/stories/{id}/export
```

**Request Body:**
```json
{
  "outputPath": ".project",        // Optional, defaults to .project in worktree
  "format": "sdd",                 // "sdd" | "json" | "markdown"
  "include": ["research", "plan", "changes", "review"]  // Optional filter
}
```

**Response:**
```json
{
  "exported": [
    { "type": "research", "path": ".project/research/research-add-caching-2026-02-03.md" },
    { "type": "plan", "path": ".project/plans/plan-add-caching-2026-02-03.md" },
    { "type": "changes", "path": ".project/changes/changes-add-caching-2026-02-03.md" }
  ]
}
```

### Export Format: Research Document

**Source:** `Story.AnalyzedContext` (JSON)

**Output:** `.project/research/research-{title-slug}-{date}.md`

```markdown
# Research: {Story.Title}

**Story ID:** {Story.Id}
**Analyzed:** {timestamp from AnalyzedContext}
**Status:** Complete

## Summary

{Extract "analysis" field from AnalyzedContext}

## Core Requirements

{Extract "coreRequirements" or parse from analysis}

## Technical Constraints

{Extract constraints from analysis}

## Files Likely Affected

{List from AnalyzedContext.affectedFiles}

## Open Questions

{If Story has open questions, list them with answers}
{Otherwise: "No explicit open questions tracked"}

## Risks

{If available, otherwise: "Risk analysis not performed"}

---
*Exported from Aura workflow {Story.Id}*
```

### Export Format: Plan Document

**Source:** `Story.ExecutionPlan` + `Story.Steps`

**Output:** `.project/plans/plan-{title-slug}-{date}.md`

```markdown
# Plan: {Story.Title}

**Story ID:** {Story.Id}
**Planned:** {timestamp}
**Steps:** {count}

## Summary

{Story.Description}

## Implementation Steps

### Step 1: {step.Name}

**Capability:** {step.Capability}
**Language:** {step.Language}

{step.Description}

**Status:** {step.Status}

---

### Step 2: ...

## Verification

- Build: {from VerificationResult or "Not yet verified"}
- Tests: {from VerificationResult}
- Lint: {from VerificationResult}

---
*Exported from Aura workflow {Story.Id}*
```

### Export Format: Changes Document

**Source:** Completed steps with output

**Output:** `.project/changes/changes-{title-slug}-{date}.md`

```markdown
# Changes: {Story.Title}

**Story ID:** {Story.Id}
**Started:** {Story.CreatedAt}
**Completed:** {Story.CompletedAt or "In Progress"}

## Progress

{For each step:}
- [x] Step 1: {name} ✅
- [x] Step 2: {name} ✅
- [ ] Step 3: {name} ⏳

## Step Details

### Step 1: {name}

**Duration:** {CompletedAt - StartedAt}
**Agent:** {AssignedAgentId}

{Parse output JSON for summary}

**Files Changed:**
{Extract from tool calls in output if available}

---

## Commits

{If git integration available, list commits in worktree}

---
*Exported from Aura workflow {Story.Id}*
```

### Implementation Notes

1. Create `IStoryExporter` service in `Aura.Module.Developer`
2. Add endpoint in `DeveloperEndpoints.cs`
3. Use worktree path if available, otherwise require outputPath
4. Parse JSON artifacts to extract structured content
5. Generate slugified filenames from story title

---

## Feature 2: Enhanced Research (Open Questions)

### Summary

Allow stories to have explicit "open questions" that the analysis phase must answer, similar to SDD backlog items.

### Data Model Changes

Add to `Story` entity:

```csharp
/// <summary>Gets or sets open questions as JSON array.</summary>
public string? OpenQuestions { get; set; }
```

**JSON Structure:**
```json
[
  {
    "question": "Should we use Redis or Memcached for caching?",
    "answered": true,
    "answer": "Redis - supports pub/sub for cache invalidation",
    "source": "Analysis of existing infrastructure"
  },
  {
    "question": "What's the cache invalidation strategy?",
    "answered": false,
    "answer": null,
    "source": null
  }
]
```

### API Changes

**Create/Update Story:**
```json
{
  "title": "Add caching to ProductService",
  "description": "...",
  "openQuestions": [
    "Should we use Redis or Memcached?",
    "What's the cache TTL strategy?",
    "How do we handle cache stampede?"
  ]
}
```

**Analysis Prompt Enhancement:**

Update `workflow-enrich.prompt` to include:

```
## Open Questions to Answer

{{#each openQuestions}}
- {{this.question}}
{{/each}}

For each open question, provide:
1. A direct answer
2. Evidence/rationale for the answer
3. Alternative options considered
```

**Analyze Response Parsing:**

Extract answers from LLM output and update `OpenQuestions` JSON.

### UI Changes

- Show open questions in story detail panel
- Indicate answered vs. unanswered
- Allow adding questions before/during analysis

---

## Feature 3: Risk Analysis in Research

### Summary

Add explicit risk identification to the analysis phase.

### Data Model Changes

Add to `Story` entity:

```csharp
/// <summary>Gets or sets identified risks as JSON array.</summary>
public string? IdentifiedRisks { get; set; }
```

**JSON Structure:**
```json
[
  {
    "risk": "Cache invalidation complexity",
    "likelihood": "Medium",
    "impact": "High",
    "mitigation": "Use write-through caching pattern"
  }
]
```

### Prompt Enhancement

Update `workflow-enrich.prompt`:

```
## Risk Analysis

Identify potential risks with this implementation:

| Risk | Likelihood (H/M/L) | Impact (H/M/L) | Mitigation |
|------|-------------------|----------------|------------|

Consider:
- Technical complexity risks
- Integration risks  
- Performance risks
- Security risks
- Maintenance risks
```

---

## Feature 4: Structured Review Checklist

### Summary

Replace pass/fail verification with a structured checklist similar to SDD's verify phase.

### Data Model Changes

Expand `VerificationResult` JSON structure:

```json
{
  "summary": "Approved with suggestions",
  "decision": "approved",  // "approved" | "changes_requested" | "rejected"
  
  "checklist": {
    "functional": {
      "requirements_met": true,
      "notes": "All 3 requirements implemented"
    },
    "code_quality": {
      "follows_guidelines": true,
      "no_lint_errors": true,
      "error_handling": true,
      "notes": null
    },
    "testing": {
      "tests_pass": true,
      "new_tests_added": true,
      "coverage_acceptable": true,
      "notes": "Added 5 unit tests"
    },
    "architecture": {
      "follows_patterns": true,
      "respects_layers": true,
      "no_unnecessary_deps": true,
      "notes": null
    }
  },
  
  "findings": {
    "must_fix": [],
    "should_fix": [
      "Consider adding XML doc comments to public methods"
    ],
    "suggestions": [
      "Could extract cache key generation to a helper"
    ]
  },
  
  "build": { "passed": true, "output": "..." },
  "tests": { "passed": true, "total": 45, "failed": 0 },
  "lint": { "passed": true, "warnings": 2 }
}
```

### API Changes

**Enhanced Verify Endpoint:**

```
POST /api/stories/{id}/verify
```

**Request Body (optional):**
```json
{
  "runBuild": true,
  "runTests": true,
  "runLint": true,
  "includeCodeReview": true  // NEW: Run agent-based code review
}
```

When `includeCodeReview: true`, run a review agent that:
1. Checks code against coding guidelines
2. Evaluates architecture alignment
3. Produces structured checklist + findings

### UI Changes

- Show checklist in verification panel
- Color-code: ✅ pass, ⚠️ warning, ❌ fail
- Expandable findings with severity tiers

---

## Feature 5: Web Search in Research (Optional)

### Summary

Allow the analysis phase to search the web for best practices, documentation, and examples.

### Approach

Add web search tool to `workflow-enrich.prompt`:

```yaml
tools:
  - file.list
  - file.read
  - file.exists
  - search.grep
  - search.web      # NEW
```

**Tool Definition:**
```csharp
public class WebSearchTool : ITool
{
    public string Name => "search.web";
    public string Description => "Search the web for documentation, best practices, and examples";
    
    // Parameters: query (string), maxResults (int, default 5)
    // Returns: Array of { title, url, snippet }
}
```

### Implementation Options

1. **Bing Search API** - Requires API key, cost per query
2. **DuckDuckGo Instant Answers** - Free, limited depth
3. **Custom search** - Curated domains (docs.microsoft.com, stackoverflow.com, etc.)

### Configuration

```json
// appsettings.json
{
  "Aura": {
    "Research": {
      "EnableWebSearch": true,
      "WebSearchProvider": "bing",
      "WebSearchApiKey": "...",
      "AllowedDomains": ["docs.microsoft.com", "learn.microsoft.com", "stackoverflow.com"]
    }
  }
}
```

---

## Implementation Priority

| Feature | Priority | Effort | Value |
|---------|----------|--------|-------|
| 1. Artifact Export | P1 | Medium | High - Bridges SDD/Aura gap |
| 4. Structured Review | P1 | Medium | High - Better validation UX |
| 2. Open Questions | P2 | Low | Medium - Guides research |
| 3. Risk Analysis | P2 | Low | Medium - Better planning |
| 5. Web Search | P3 | High | Medium - Nice to have |

### Phase 1 (MVP)
- Artifact Export (research, plan, changes)
- Structured Review Checklist

### Phase 2
- Open Questions support
- Risk Analysis in research
- Enhanced export (include Q&A and risks)

### Phase 3
- Web Search integration
- Full SDD template compliance

---

## Success Criteria

- [ ] User can export workflow as SDD-compatible markdown files
- [ ] Exported files follow SDD naming conventions
- [ ] Verification shows structured checklist, not just pass/fail
- [ ] Open questions (if defined) appear in research export with answers
- [ ] Risks (if identified) appear in research export
- [ ] Documentation maps Aura phases to SDD phases

## Open Questions

1. **Should export happen automatically on completion?** 
   - Or always on-demand?
   - Config option: `"AutoExportOnComplete": true`

2. **Where to export when no worktree?**
   - Require outputPath in request?
   - Use temp directory and return download link?

3. **How to handle re-planning?**
   - Overwrite previous plan export?
   - Create versioned files (plan-v1, plan-v2)?

4. **Should web search be agent-controlled or automatic?**
   - Agent decides when to search (current design)
   - Always search certain topics (e.g., "best practices for {technology}")

---

## References

- [SDD Philosophy](../../anvil/.sdd/philosophy.md)
- [Aura-SDD Mapping](sdd-mapping.md)
- [Workflow Lifecycle](workflows.md)
