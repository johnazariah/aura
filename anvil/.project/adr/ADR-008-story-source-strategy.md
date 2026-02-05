---
title: "ADR-008: Story Source Strategy"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["testing", "stories", "extensibility", "github"]
supersedes: ""
superseded_by: ""
---

# ADR-008: Story Source Strategy

## Status

Accepted

## Context

Anvil tests Aura by running "stories" through it. The workflow is:

```
Story Source (specification)
    → Aura generates code
    → PR created (output)
    → Anvil validates the PR (build, tests, expected files)
```

Stories can come from multiple sources:

| Source | Use Case | Timeline |
|--------|----------|----------|
| **Local markdown files** | Development, quick iteration | Phase 1 (Now) |
| **GitHub Issues** | Real-world end-to-end workflow | Phase 2 (Future) |

**Note:** PRs are the *output* of code generation, not a story source. Anvil validates that the generated PR meets the story's acceptance criteria.

We need an extensible architecture that:
- Starts simple (markdown files in a folder)
- Grows to support GitHub Issue integration
- Tracks metadata about each story source
- Links story results to their source for traceability

## Decision

We adopt a **pluggable Story Source** architecture with an abstraction over where stories come from.

### Story Source Abstraction

```csharp
public interface IStorySource
{
    string SourceType { get; }  // "file", "github-issue"
    
    Task<Result<IReadOnlyList<StoryDescriptor>, StoryError>> ListStoriesAsync(
        StoryFilter? filter, 
        CancellationToken ct);
    
    Task<Result<Story, StoryError>> LoadStoryAsync(
        string storyId, 
        CancellationToken ct);
}

public record StoryDescriptor(
    string Id,
    string Title,
    string SourceType,
    string SourceLocation,  // File path or issue URL
    IReadOnlyDictionary<string, string> Metadata
);

public record Story(
    string Id,
    string Title,
    string Description,
    string SourceType,
    string SourceLocation,
    StoryContent Content,
    IReadOnlyDictionary<string, string> Metadata
);

public record StoryContent(
    string RawText,           // The actual story specification
    string? Language,         // Target language (csharp, python, typescript)
    string? Category,         // greenfield, brownfield
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> ExpectedFiles,      // Files that should be generated
    IReadOnlyList<string> AcceptanceCriteria  // Validation checks
);
```

### Validation Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Story Source   │────►│      Aura       │────►│   PR Created    │────►│ Anvil Validates │
│  (file/issue)   │     │ (generates code)│     │   (output)      │     │  (build/test)   │
└─────────────────┘     └─────────────────┘     └─────────────────┘     └─────────────────┘
        │                                                                       │
        │                                                                       │
        └───────────────── Story defines acceptance criteria ──────────────────►┘
```

### Phase 1: File-Based Stories

Stories stored as markdown files with YAML frontmatter:

```
anvil/
└── stories/
    ├── greenfield/
    │   ├── cli-hello-world.md
    │   ├── rest-api-basic.md
    │   └── library-with-tests.md
    └── brownfield/
        ├── add-feature.md
        └── refactor-rename.md
```

#### Story File Format

```markdown
---
id: cli-hello-world
title: CLI Hello World
language: csharp
category: greenfield
tags: [cli, beginner]
expected_files:
  - Program.cs
  - "*.csproj"
validation:
  - build: true
  - run_output: "Hello, World!"
---

# CLI Hello World

Create a simple command-line application that prints "Hello, World!" to the console.

## Requirements

- The application should be a .NET console application
- It should print exactly "Hello, World!" followed by a newline
- The project should build without warnings

## Acceptance Criteria

- [ ] Project compiles successfully
- [ ] Running the app outputs "Hello, World!"
- [ ] No build warnings
```

#### File Source Implementation

```csharp
public class FileStorySource : IStorySource
{
    private readonly string _storiesPath;
    private readonly ILogger<FileStorySource> _logger;

    public string SourceType => "file";

    public FileStorySource(IOptions<AnvilOptions> options, ILogger<FileStorySource> logger)
    {
        _storiesPath = options.Value.StoriesPath;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<StoryDescriptor>, StoryError>> ListStoriesAsync(
        StoryFilter? filter, 
        CancellationToken ct)
    {
        var files = Directory.GetFiles(_storiesPath, "*.md", SearchOption.AllDirectories);
        var descriptors = new List<StoryDescriptor>();

        foreach (var file in files)
        {
            var frontmatter = await ParseFrontmatterAsync(file, ct);
            if (frontmatter.IsSuccess)
            {
                descriptors.Add(new StoryDescriptor(
                    Id: frontmatter.Value.Id,
                    Title: frontmatter.Value.Title,
                    SourceType: "file",
                    SourceLocation: file,
                    Metadata: frontmatter.Value.Metadata
                ));
            }
        }

        return Result<IReadOnlyList<StoryDescriptor>, StoryError>.Ok(descriptors);
    }
}
```

### Phase 2: GitHub Issue Integration (Future)

```csharp
public class GitHubIssueStorySource : IStorySource
{
    public string SourceType => "github-issue";

    public async Task<Result<Story, StoryError>> LoadStoryAsync(string issueUrl, CancellationToken ct)
    {
        // Parse issue number from URL
        // Fetch issue body via GitHub API
        // Extract story content from issue description
        // Parse acceptance criteria from issue body
        // Return structured Story
    }
}
```

End-to-end flow with GitHub:
```
1. Developer creates GitHub Issue with story specification
2. Anvil reads story from GitHub Issue
3. Aura generates code based on story
4. Aura creates PR linked to the Issue
5. Anvil validates the PR against story's acceptance criteria
6. Anvil reports results (pass/fail) 
```

### Composite Story Source

Aggregate multiple sources:

```csharp
public class CompositeStorySource : IStorySource
{
    private readonly IReadOnlyList<IStorySource> _sources;

    public string SourceType => "composite";

    public async Task<Result<IReadOnlyList<StoryDescriptor>, StoryError>> ListStoriesAsync(
        StoryFilter? filter, 
        CancellationToken ct)
    {
        var allStories = new List<StoryDescriptor>();
        
        foreach (var source in _sources)
        {
            var result = await source.ListStoriesAsync(filter, ct);
            if (result.IsSuccess)
            {
                allStories.AddRange(result.Value);
            }
        }

        return Result<IReadOnlyList<StoryDescriptor>, StoryError>.Ok(allStories);
    }
}
```

### Metadata Storage

Story results include source metadata for traceability:

```csharp
public record StoryResult
{
    public required string StoryId { get; init; }
    public required string SourceType { get; init; }      // "file", "github-issue"
    public required string SourceLocation { get; init; }  // Path or Issue URL
    public required string? PrUrl { get; init; }          // The generated PR (output)
    public required StoryStatus Status { get; init; }
    public required DateTimeOffset ExecutedAt { get; init; }
    public required TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string>? GeneratedFiles { get; init; }
    public IReadOnlyList<ValidationResult>? ValidationResults { get; init; }
}
```

### CLI Usage

```bash
# Run stories from local files (default)
anvil run stories/greenfield/cli-hello-world.md

# Run all stories in a directory
anvil run stories/greenfield/

# Future: Run from GitHub issue
anvil run --source github-issue https://github.com/owner/repo/issues/123

# List available stories
anvil list --source file
anvil list --source github-issue --repo owner/repo --label "story"
```

## Consequences

**Positive**
- **POS-001**: Simple start with markdown files
- **POS-002**: Extensible to GitHub Issues
- **POS-003**: Clear separation: stories are input, PRs are output
- **POS-004**: Metadata links results back to source
- **POS-005**: Story format is human-readable and version-controlled

**Negative**
- **NEG-001**: Abstraction adds complexity
- **NEG-002**: GitHub integration requires API authentication
- **NEG-003**: Remote sources may be slower or rate-limited

## Alternatives Considered

### Alternative 1: YAML-Only Story Format
- **Description**: Use YAML instead of Markdown for stories
- **Rejection Reason**: Markdown is more readable for story prose; YAML frontmatter gives best of both

### Alternative 2: Hardcoded Stories
- **Description**: Stories defined in C# code
- **Rejection Reason**: Not editable without recompile; poor for iteration

### Alternative 3: Database-Stored Stories
- **Description**: Store stories in SQLite alongside results
- **Rejection Reason**: Harder to edit; loses version control benefits

## Implementation Notes

- **IMP-001**: Start with `FileStorySource` only; add GitHub source later
- **IMP-002**: Use YamlDotNet for parsing frontmatter
- **IMP-003**: Story IDs must be unique across all sources
- **IMP-004**: GitHub source will need `Octokit` library
- **IMP-005**: Cache GitHub responses to avoid rate limits
- **IMP-006**: Store source metadata in `StoryResults` table for regression tracking

## References

- [YAML Frontmatter Parsing](https://github.com/aaubry/YamlDotNet)
- [Octokit.NET](https://github.com/octokit/octokit.net)
- [ADR-005: Database Strategy](ADR-005-database-strategy.md)
