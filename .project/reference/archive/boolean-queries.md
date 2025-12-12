# Task: Boolean Query Parser

## Overview

Implement a query parser that supports boolean operators (AND, OR) and type prefixes (class:, method:, file:) for more expressive code graph searches.

## Parent Spec

`.project/spec/15-graph-and-indexing-enhancements.md` - Gap 7

## Goals

1. Parse human-readable queries with operators
2. Support type filtering via prefixes
3. Combine with vector similarity search
4. Backward compatible with existing search

## Query Syntax

### Basic Queries

```
WorkflowService                     # Simple name match
"WorkflowService"                   # Exact phrase
workflow service                    # Both terms (implicit AND)
```

### Boolean Operators

```
workflow OR service                 # Either term
workflow AND service                # Both terms (explicit)
workflow -legacy                    # Exclude "legacy"
(git OR svn) AND worktree          # Grouping
```

### Type Prefixes

```
class:WorkflowService              # Only classes
method:ExecuteAsync                # Only methods
file:Program.cs                    # Only files
ns:Aura.Foundation                 # Namespace
interface:ICodeGraphService        # Interfaces
prop:ConnectionString              # Properties
```

### Combined

```
class:Service AND method:Execute   # Methods in Service classes
file:*.cs -file:*Tests.cs          # C# files, not tests
method:Async OR method:Task        # Async methods
```

## Data Model

### Query AST

**File:** `src/Aura.Foundation/Rag/Query/QueryAst.cs`

```csharp
namespace Aura.Foundation.Rag.Query;

/// <summary>
/// Abstract syntax tree for code graph queries.
/// </summary>
public abstract record QueryNode;

/// <summary>A single search term.</summary>
public record TermNode(string Value, bool Negated = false) : QueryNode;

/// <summary>A type-prefixed term (class:Foo).</summary>
public record TypedTermNode(CodeNodeType NodeType, string Value, bool Negated = false) : QueryNode;

/// <summary>A file pattern term (file:*.cs).</summary>
public record FilePatternNode(string Pattern, bool Negated = false) : QueryNode;

/// <summary>Binary AND.</summary>
public record AndNode(QueryNode Left, QueryNode Right) : QueryNode;

/// <summary>Binary OR.</summary>
public record OrNode(QueryNode Left, QueryNode Right) : QueryNode;

/// <summary>Parenthesized group.</summary>
public record GroupNode(QueryNode Inner) : QueryNode;

/// <summary>Empty query (match all).</summary>
public record EmptyNode : QueryNode;
```

### Parse Result

```csharp
public record ParseResult
{
    public required QueryNode Ast { get; init; }
    public List<string> Warnings { get; init; } = new();
    public bool IsEmpty => Ast is EmptyNode;
}
```

## Query Parser

**File:** `src/Aura.Foundation/Rag/Query/QueryParser.cs`

```csharp
public sealed class QueryParser
{
    private static readonly Dictionary<string, CodeNodeType> TypePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["class"] = CodeNodeType.Class,
        ["method"] = CodeNodeType.Method,
        ["property"] = CodeNodeType.Property,
        ["prop"] = CodeNodeType.Property,
        ["field"] = CodeNodeType.Field,
        ["interface"] = CodeNodeType.Interface,
        ["enum"] = CodeNodeType.Enum,
        ["struct"] = CodeNodeType.Struct,
        ["record"] = CodeNodeType.Record,
        ["file"] = CodeNodeType.File,
        ["ns"] = CodeNodeType.Namespace,
        ["namespace"] = CodeNodeType.Namespace,
        ["content"] = CodeNodeType.Content,
    };

    public ParseResult Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ParseResult { Ast = new EmptyNode() };
        }

        var tokens = Tokenize(query);
        var ast = ParseExpression(tokens, 0, out _);
        
        return new ParseResult { Ast = ast };
    }

    private List<Token> Tokenize(string query)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < query.Length)
        {
            // Skip whitespace
            while (i < query.Length && char.IsWhiteSpace(query[i]))
                i++;

            if (i >= query.Length) break;

            // Operators and grouping
            switch (query[i])
            {
                case '(':
                    tokens.Add(new Token(TokenType.LParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenType.RParen, ")"));
                    i++;
                    continue;
                case '-':
                    tokens.Add(new Token(TokenType.Not, "-"));
                    i++;
                    continue;
            }

            // Quoted string
            if (query[i] == '"')
            {
                var start = ++i;
                while (i < query.Length && query[i] != '"') i++;
                tokens.Add(new Token(TokenType.Term, query[start..i]));
                if (i < query.Length) i++; // Skip closing quote
                continue;
            }

            // Word (may include type prefix)
            var wordStart = i;
            while (i < query.Length && !char.IsWhiteSpace(query[i]) && query[i] != '(' && query[i] != ')')
                i++;

            var word = query[wordStart..i];

            // Check for boolean operators
            if (word.Equals("AND", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new Token(TokenType.And, word));
            }
            else if (word.Equals("OR", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new Token(TokenType.Or, word));
            }
            else
            {
                tokens.Add(new Token(TokenType.Term, word));
            }
        }

        return tokens;
    }

    private QueryNode ParseExpression(List<Token> tokens, int pos, out int newPos)
    {
        var left = ParseUnary(tokens, pos, out pos);

        while (pos < tokens.Count)
        {
            if (tokens[pos].Type == TokenType.Or)
            {
                pos++;
                var right = ParseUnary(tokens, pos, out pos);
                left = new OrNode(left, right);
            }
            else if (tokens[pos].Type == TokenType.And)
            {
                pos++;
                var right = ParseUnary(tokens, pos, out pos);
                left = new AndNode(left, right);
            }
            else if (tokens[pos].Type == TokenType.Term || tokens[pos].Type == TokenType.LParen)
            {
                // Implicit AND
                var right = ParseUnary(tokens, pos, out pos);
                left = new AndNode(left, right);
            }
            else
            {
                break;
            }
        }

        newPos = pos;
        return left;
    }

    private QueryNode ParseUnary(List<Token> tokens, int pos, out int newPos)
    {
        var negated = false;
        
        while (pos < tokens.Count && tokens[pos].Type == TokenType.Not)
        {
            negated = !negated;
            pos++;
        }

        var node = ParsePrimary(tokens, pos, out pos);

        if (negated)
        {
            node = node switch
            {
                TermNode t => t with { Negated = true },
                TypedTermNode t => t with { Negated = true },
                FilePatternNode f => f with { Negated = true },
                _ => node, // Can't negate groups directly
            };
        }

        newPos = pos;
        return node;
    }

    private QueryNode ParsePrimary(List<Token> tokens, int pos, out int newPos)
    {
        if (pos >= tokens.Count)
        {
            newPos = pos;
            return new EmptyNode();
        }

        // Grouped expression
        if (tokens[pos].Type == TokenType.LParen)
        {
            var inner = ParseExpression(tokens, pos + 1, out pos);
            if (pos < tokens.Count && tokens[pos].Type == TokenType.RParen)
                pos++;
            newPos = pos;
            return new GroupNode(inner);
        }

        // Term (possibly typed)
        if (tokens[pos].Type == TokenType.Term)
        {
            var term = tokens[pos].Value;
            pos++;
            newPos = pos;

            // Check for type prefix
            var colonIndex = term.IndexOf(':');
            if (colonIndex > 0)
            {
                var prefix = term[..colonIndex];
                var value = term[(colonIndex + 1)..];

                if (prefix.Equals("file", StringComparison.OrdinalIgnoreCase))
                {
                    return new FilePatternNode(value);
                }

                if (TypePrefixes.TryGetValue(prefix, out var nodeType))
                {
                    return new TypedTermNode(nodeType, value);
                }
            }

            return new TermNode(term);
        }

        newPos = pos;
        return new EmptyNode();
    }

    private enum TokenType { Term, And, Or, Not, LParen, RParen }
    private record Token(TokenType Type, string Value);
}
```

## Query Executor

**File:** `src/Aura.Foundation/Rag/Query/QueryExecutor.cs`

```csharp
public sealed class QueryExecutor
{
    private readonly AuraDbContext _dbContext;
    private readonly IEmbeddingProvider _embeddingProvider;

    public async Task<List<CodeNode>> ExecuteAsync(
        QueryNode ast,
        string workspacePath,
        int limit = 50,
        CancellationToken ct = default)
    {
        // For simple term queries, use vector similarity
        // For complex boolean queries, build SQL predicates

        return ast switch
        {
            EmptyNode => await GetAllNodes(workspacePath, limit, ct),
            TermNode t => await SearchByTerm(workspacePath, t, limit, ct),
            TypedTermNode t => await SearchByTypedTerm(workspacePath, t, limit, ct),
            FilePatternNode f => await SearchByFilePattern(workspacePath, f, limit, ct),
            AndNode and => await ExecuteAnd(workspacePath, and, limit, ct),
            OrNode or => await ExecuteOr(workspacePath, or, limit, ct),
            GroupNode g => await ExecuteAsync(g.Inner, workspacePath, limit, ct),
            _ => throw new NotSupportedException($"Unknown node type: {ast.GetType().Name}"),
        };
    }

    private async Task<List<CodeNode>> SearchByTerm(
        string workspacePath,
        TermNode term,
        int limit,
        CancellationToken ct)
    {
        // Hybrid search: name match + vector similarity
        var embedding = await _embeddingProvider.GetEmbeddingAsync(term.Value, ct);

        var query = _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath);

        if (term.Negated)
        {
            query = query.Where(n => 
                !EF.Functions.ILike(n.Name, $"%{term.Value}%") &&
                !EF.Functions.ILike(n.FullName ?? "", $"%{term.Value}%"));
        }
        else
        {
            // Combine text match score with vector similarity
            query = query
                .OrderByDescending(n => 
                    (EF.Functions.ILike(n.Name, $"%{term.Value}%") ? 1 : 0) +
                    (n.Embedding != null 
                        ? 1.0 - n.Embedding.CosineDistance(new Pgvector.Vector(embedding))
                        : 0))
                .Take(limit);
        }

        return await query.ToListAsync(ct);
    }

    private async Task<List<CodeNode>> SearchByTypedTerm(
        string workspacePath,
        TypedTermNode term,
        int limit,
        CancellationToken ct)
    {
        var query = _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath)
            .Where(n => n.NodeType == term.NodeType);

        if (!string.IsNullOrEmpty(term.Value) && term.Value != "*")
        {
            if (term.Negated)
            {
                query = query.Where(n => !EF.Functions.ILike(n.Name, $"%{term.Value}%"));
            }
            else
            {
                query = query.Where(n => EF.Functions.ILike(n.Name, $"%{term.Value}%"));
            }
        }

        return await query.Take(limit).ToListAsync(ct);
    }

    private async Task<List<CodeNode>> SearchByFilePattern(
        string workspacePath,
        FilePatternNode pattern,
        int limit,
        CancellationToken ct)
    {
        // Convert glob pattern to SQL LIKE
        var sqlPattern = pattern.Pattern
            .Replace("*", "%")
            .Replace("?", "_");

        var query = _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath);

        if (pattern.Negated)
        {
            query = query.Where(n => !EF.Functions.ILike(n.FilePath ?? "", sqlPattern));
        }
        else
        {
            query = query.Where(n => EF.Functions.ILike(n.FilePath ?? "", sqlPattern));
        }

        return await query.Take(limit).ToListAsync(ct);
    }

    private async Task<List<CodeNode>> ExecuteAnd(
        string workspacePath,
        AndNode and,
        int limit,
        CancellationToken ct)
    {
        var leftResults = await ExecuteAsync(and.Left, workspacePath, limit * 2, ct);
        var rightResults = await ExecuteAsync(and.Right, workspacePath, limit * 2, ct);

        var rightIds = rightResults.Select(n => n.Id).ToHashSet();
        return leftResults
            .Where(n => rightIds.Contains(n.Id))
            .Take(limit)
            .ToList();
    }

    private async Task<List<CodeNode>> ExecuteOr(
        string workspacePath,
        OrNode or,
        int limit,
        CancellationToken ct)
    {
        var leftResults = await ExecuteAsync(or.Left, workspacePath, limit, ct);
        var rightResults = await ExecuteAsync(or.Right, workspacePath, limit, ct);

        var seen = new HashSet<Guid>();
        var combined = new List<CodeNode>();

        foreach (var node in leftResults.Concat(rightResults))
        {
            if (seen.Add(node.Id))
            {
                combined.Add(node);
                if (combined.Count >= limit) break;
            }
        }

        return combined;
    }

    private async Task<List<CodeNode>> GetAllNodes(
        string workspacePath,
        int limit,
        CancellationToken ct)
    {
        return await _dbContext.CodeNodes
            .Where(n => n.WorkspacePath == workspacePath)
            .Take(limit)
            .ToListAsync(ct);
    }
}
```

## Integration with ICodeGraphService

**File:** Update `src/Aura.Foundation/Rag/ICodeGraphService.cs`

```csharp
public interface ICodeGraphService
{
    // Existing methods...
    
    /// <summary>
    /// Searches using boolean query syntax.
    /// </summary>
    Task<List<CodeNode>> SearchAsync(
        string query,
        string workspacePath,
        int limit = 50,
        CancellationToken ct = default);
}
```

**Implementation:**

```csharp
public async Task<List<CodeNode>> SearchAsync(
    string query,
    string workspacePath,
    int limit,
    CancellationToken ct)
{
    var parser = new QueryParser();
    var parseResult = parser.Parse(query);
    
    if (parseResult.IsEmpty)
    {
        // Fall back to existing behavior
        return await GetAllNodesAsync(workspacePath, limit, ct);
    }

    var executor = new QueryExecutor(_dbContext, _embeddingProvider);
    return await executor.ExecuteAsync(parseResult.Ast, workspacePath, limit, ct);
}
```

## API Updates

**File:** Update search endpoints

```csharp
[HttpGet("search")]
public async Task<ActionResult<List<CodeNodeDto>>> Search(
    [FromQuery] string q,
    [FromQuery] string workspacePath,
    [FromQuery] int limit = 50,
    CancellationToken ct = default)
{
    // Now supports: "class:Service AND method:Execute"
    var results = await _codeGraphService.SearchAsync(q, workspacePath, limit, ct);
    return Ok(results.Select(CodeNodeDto.From));
}
```

## Testing

### Parser Tests

**File:** `tests/Aura.Foundation.Tests/Rag/Query/QueryParserTests.cs`

```csharp
public class QueryParserTests
{
    private readonly QueryParser _parser = new();

    [Theory]
    [InlineData("foo", typeof(TermNode))]
    [InlineData("class:Foo", typeof(TypedTermNode))]
    [InlineData("file:*.cs", typeof(FilePatternNode))]
    public void Parse_SingleTerm_ReturnsCorrectType(string query, Type expectedType)
    {
        var result = _parser.Parse(query);
        Assert.IsType(expectedType, result.Ast);
    }

    [Fact]
    public void Parse_OrExpression_ReturnsOrNode()
    {
        var result = _parser.Parse("foo OR bar");
        Assert.IsType<OrNode>(result.Ast);
    }

    [Fact]
    public void Parse_AndExpression_ReturnsAndNode()
    {
        var result = _parser.Parse("foo AND bar");
        Assert.IsType<AndNode>(result.Ast);
    }

    [Fact]
    public void Parse_ImplicitAnd_ReturnsAndNode()
    {
        var result = _parser.Parse("foo bar");
        Assert.IsType<AndNode>(result.Ast);
    }

    [Fact]
    public void Parse_Negation_SetsNegatedFlag()
    {
        var result = _parser.Parse("-legacy");
        var term = Assert.IsType<TermNode>(result.Ast);
        Assert.True(term.Negated);
    }

    [Fact]
    public void Parse_GroupedExpression_ReturnsGroupNode()
    {
        var result = _parser.Parse("(foo OR bar) AND baz");
        var and = Assert.IsType<AndNode>(result.Ast);
        Assert.IsType<GroupNode>(and.Left);
    }

    [Fact]
    public void Parse_TypedTerm_ExtractsTypeAndValue()
    {
        var result = _parser.Parse("class:WorkflowService");
        var typed = Assert.IsType<TypedTermNode>(result.Ast);
        Assert.Equal(CodeNodeType.Class, typed.NodeType);
        Assert.Equal("WorkflowService", typed.Value);
    }
}
```

### Executor Integration Tests

- Test AND reduces result set
- Test OR expands result set
- Test negation excludes matches
- Test type prefix filters correctly
- Test file patterns with wildcards

## Rollout Plan

1. **Phase 1**: Implement QueryParser with unit tests
2. **Phase 2**: Implement QueryExecutor
3. **Phase 3**: Integrate with ICodeGraphService.SearchAsync
4. **Phase 4**: Update API endpoints
5. **Phase 5**: Document query syntax in API docs

## Dependencies

- Existing `AuraDbContext`
- Existing `IEmbeddingProvider`
- EF Core PostgreSQL extensions

## Estimated Effort

- **Medium complexity**, **Medium effort**
- Parser is straightforward; executor requires careful SQL generation

## Success Criteria

- [ ] Parser handles all documented syntax
- [ ] `class:Foo` returns only classes
- [ ] `A OR B` returns union of results
- [ ] `A AND B` returns intersection
- [ ] `-term` excludes matches
- [ ] Grouped expressions work correctly
- [ ] Backward compatible (simple queries work as before)
- [ ] Performance: < 100ms for typical queries
