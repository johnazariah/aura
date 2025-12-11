# Implementation Plan: Dependency Graph Edges

**Status**: Not Started  
**Priority**: High  
**Estimated Effort**: 4-6 hours  
**Prerequisites**: TreeSitter Phase 2a (✅ Complete)

## Overview

Use the `Imports` field extracted by TreeSitter to create edges in the code graph, enabling queries like "find all files that import X".

## Current State

TreeSitter now extracts imports for each file:

```csharp
// In SemanticChunk
public IReadOnlyList<string>? Imports { get; init; }

// Examples of extracted imports:
// Python: ["os", "sys", "typing.List", "models.User"]
// TypeScript: ["react", "./components/Button", "@types/node"]
// Go: ["fmt", "github.com/user/repo/pkg"]
```

However, these imports are stored in `RagChunk` but not used to create graph edges.

## Goals

1. Create `Imports` edge type in `CodeEdgeType`
2. During indexing, create edges from file to imported modules
3. Resolve relative imports to actual file paths
4. Support "find all files that import X" queries
5. Build dependency graph visualization data

## Implementation Steps

### Step 1: Add Edge Type (15 min)

**File**: `src/Aura.Foundation/Data/Entities/CodeEdge.cs`

```csharp
public enum CodeEdgeType
{
    // Existing...
    Contains,
    Inherits,
    Implements,
    Calls,
    References,
    
    // NEW
    /// <summary>File imports this module/package.</summary>
    Imports,
}
```

### Step 2: Create Import Edge Builder (1-2 hours)

**File**: `src/Aura.Module.Developer/Services/ImportEdgeBuilder.cs`

```csharp
namespace Aura.Module.Developer.Services;

public interface IImportEdgeBuilder
{
    /// <summary>
    /// Create edges from a file to its imported modules.
    /// </summary>
    Task<IReadOnlyList<CodeEdge>> BuildImportEdgesAsync(
        string filePath,
        IReadOnlyList<string> imports,
        string language,
        string repositoryPath,
        CancellationToken ct = default);
}

public class ImportEdgeBuilder : IImportEdgeBuilder
{
    private readonly AuraDbContext _db;
    private readonly ILogger<ImportEdgeBuilder> _logger;

    public ImportEdgeBuilder(AuraDbContext db, ILogger<ImportEdgeBuilder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CodeEdge>> BuildImportEdgesAsync(
        string filePath,
        IReadOnlyList<string> imports,
        string language,
        string repositoryPath,
        CancellationToken ct = default)
    {
        var edges = new List<CodeEdge>();
        
        // Get or create the source file node
        var sourceNode = await GetOrCreateFileNodeAsync(filePath, repositoryPath, ct);
        
        foreach (var import in imports)
        {
            var resolvedPath = ResolveImport(import, filePath, language, repositoryPath);
            
            if (resolvedPath != null)
            {
                // Relative import - link to actual file
                var targetNode = await GetOrCreateFileNodeAsync(resolvedPath, repositoryPath, ct);
                edges.Add(CreateEdge(sourceNode.Id, targetNode.Id, CodeEdgeType.Imports, import));
            }
            else
            {
                // External package - create package node
                var packageNode = await GetOrCreatePackageNodeAsync(import, language, repositoryPath, ct);
                edges.Add(CreateEdge(sourceNode.Id, packageNode.Id, CodeEdgeType.Imports, import));
            }
        }
        
        return edges;
    }

    private string? ResolveImport(string import, string sourcePath, string language, string repoPath)
    {
        return language switch
        {
            "python" => ResolvePythonImport(import, sourcePath, repoPath),
            "typescript" or "javascript" => ResolveTypeScriptImport(import, sourcePath, repoPath),
            "go" => ResolveGoImport(import, sourcePath, repoPath),
            _ => null // External package
        };
    }

    private string? ResolvePythonImport(string import, string sourcePath, string repoPath)
    {
        // Relative: "from .models import User" → "./models.py"
        // Absolute within project: "from myproject.models import User"
        if (import.StartsWith("."))
        {
            var dir = Path.GetDirectoryName(sourcePath) ?? repoPath;
            var relative = import.TrimStart('.');
            var candidate = Path.Combine(dir, relative.Replace('.', Path.DirectorySeparatorChar) + ".py");
            return File.Exists(candidate) ? candidate : null;
        }
        
        // Try to find in project
        var parts = import.Split('.');
        var searchPath = Path.Combine(repoPath, string.Join(Path.DirectorySeparatorChar, parts) + ".py");
        return File.Exists(searchPath) ? searchPath : null;
    }

    private string? ResolveTypeScriptImport(string import, string sourcePath, string repoPath)
    {
        if (!import.StartsWith(".") && !import.StartsWith("/"))
            return null; // node_modules package
        
        var dir = Path.GetDirectoryName(sourcePath) ?? repoPath;
        var extensions = new[] { ".ts", ".tsx", ".js", ".jsx", "/index.ts", "/index.tsx", "/index.js" };
        
        foreach (var ext in extensions)
        {
            var candidate = Path.Combine(dir, import + ext);
            if (File.Exists(candidate))
                return candidate;
        }
        
        return null;
    }

    private string? ResolveGoImport(string import, string sourcePath, string repoPath)
    {
        // Go uses full module paths - check if it's internal
        var goMod = FindGoMod(repoPath);
        if (goMod == null) return null;
        
        var moduleName = ParseGoModuleName(goMod);
        if (import.StartsWith(moduleName))
        {
            var relative = import[moduleName.Length..].TrimStart('/');
            var candidate = Path.Combine(repoPath, relative);
            return Directory.Exists(candidate) ? candidate : null;
        }
        
        return null; // External module
    }
}
```

### Step 3: Add Package Node Type (30 min)

**File**: `src/Aura.Foundation/Data/Entities/CodeNode.cs`

```csharp
public enum CodeNodeType
{
    // Existing...
    File,
    Namespace,
    Class,
    Interface,
    Struct,
    Enum,
    Method,
    Property,
    Field,
    
    // NEW
    /// <summary>External package/module (npm, pip, go mod).</summary>
    Package,
}
```

### Step 4: Wire into Indexing Pipeline (1-2 hours)

**File**: `src/Aura.Module.Developer/Services/CodeGraphIndexer.cs`

Update `IndexFileAsync` to call `ImportEdgeBuilder`:

```csharp
public async Task IndexFileAsync(string filePath, IReadOnlyList<SemanticChunk> chunks, CancellationToken ct)
{
    // Existing code graph building...
    
    // NEW: Build import edges from chunks
    var allImports = chunks
        .Where(c => c.Imports?.Count > 0)
        .SelectMany(c => c.Imports!)
        .Distinct()
        .ToList();
    
    if (allImports.Count > 0)
    {
        var language = DetectLanguage(filePath);
        var importEdges = await _importEdgeBuilder.BuildImportEdgesAsync(
            filePath, allImports, language, _repositoryPath, ct);
        
        await _db.CodeEdges.AddRangeAsync(importEdges, ct);
        await _db.SaveChangesAsync(ct);
    }
}
```

### Step 5: Add Query Methods (1 hour)

**File**: `src/Aura.Foundation/Rag/ICodeGraphService.cs`

```csharp
public interface ICodeGraphService
{
    // Existing...
    
    /// <summary>Find all files that import the given module.</summary>
    Task<IReadOnlyList<CodeNode>> FindImportersAsync(
        string modulePath, 
        string repositoryPath,
        CancellationToken ct = default);
    
    /// <summary>Get all imports for a file.</summary>
    Task<IReadOnlyList<CodeNode>> GetImportsAsync(
        string filePath,
        string repositoryPath,
        CancellationToken ct = default);
    
    /// <summary>Get the full dependency tree for a file.</summary>
    Task<DependencyTree> GetDependencyTreeAsync(
        string filePath,
        string repositoryPath,
        int maxDepth = 3,
        CancellationToken ct = default);
}

public record DependencyTree(
    CodeNode Root,
    IReadOnlyList<DependencyTree> Dependencies);
```

### Step 6: Add API Endpoint (30 min)

**File**: `src/Aura.Api/Program.cs`

```csharp
// Find all files that import a module
app.MapGet("/api/graph/importers", async (
    string modulePath,
    string repositoryPath,
    ICodeGraphService graphService,
    CancellationToken ct) =>
{
    var importers = await graphService.FindImportersAsync(modulePath, repositoryPath, ct);
    return Results.Ok(new { importers = importers.Select(n => new { n.Id, n.Name, n.FilePath }) });
});

// Get imports for a file
app.MapGet("/api/graph/imports", async (
    string filePath,
    string repositoryPath,
    ICodeGraphService graphService,
    CancellationToken ct) =>
{
    var imports = await graphService.GetImportsAsync(filePath, repositoryPath, ct);
    return Results.Ok(new { imports = imports.Select(n => new { n.Id, n.Name, n.NodeType }) });
});
```

### Step 7: Add Tests (1 hour)

**File**: `tests/Aura.Module.Developer.Tests/Services/ImportEdgeBuilderTests.cs`

```csharp
public class ImportEdgeBuilderTests
{
    [Fact]
    public void ResolvePythonRelativeImport_ShouldFindLocalFile()
    {
        // from .models import User → ./models.py
    }

    [Fact]
    public void ResolveTypeScriptRelativeImport_ShouldTryExtensions()
    {
        // import { Button } from './Button' → ./Button.tsx
    }

    [Fact]
    public void ExternalPackage_ShouldCreatePackageNode()
    {
        // import React from 'react' → Package node, not file
    }
}
```

## Database Migration

```sql
-- No schema change needed - CodeEdgeType is an enum stored as int
-- Just add index for faster import queries

CREATE INDEX idx_code_edges_imports 
ON code_edges (source_id, edge_type) 
WHERE edge_type = 5; -- Imports enum value
```

## Acceptance Criteria

- [ ] `Imports` edge type added to `CodeEdgeType`
- [ ] `Package` node type added to `CodeNodeType`
- [ ] `ImportEdgeBuilder` resolves relative imports for Python, TypeScript, Go
- [ ] External packages create `Package` nodes
- [ ] `FindImportersAsync` returns all files importing a module
- [ ] `GetImportsAsync` returns all imports for a file
- [ ] API endpoints work
- [ ] Unit tests pass

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Complex import resolution | Start with simple cases, iterate |
| Circular imports | Don't follow edges recursively without cycle detection |
| Large dependency trees | Limit depth, paginate results |

## Related Tasks

- `treesitter-ingesters.md` Phase 3
- `gap-03-enrichment.md` (UsesType edges)
