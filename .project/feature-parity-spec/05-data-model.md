# Data Model Specification

**Status:** Reference Documentation
**Created:** 2026-01-28

The data layer uses PostgreSQL with the pgvector extension for vector similarity search. Entity Framework Core manages the schema and migrations.

## Database Contexts

### Foundation Context (`AuraDbContext`)

Contains core entities shared by all modules:
- Conversations and messages
- RAG chunks with embeddings
- Code graph (nodes and edges)
- Workspaces
- Agent executions

### Developer Context (`DeveloperDbContext`)

Extends `AuraDbContext` with developer-specific entities:
- Stories
- Story steps
- Story tasks

---

## 1. Foundation Entities

### 1.1 Conversation

```sql
CREATE TABLE conversations (
    id UUID PRIMARY KEY,
    title VARCHAR(500),
    agent_id VARCHAR(100),
    repository_path VARCHAR(1000),
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_conversations_agent_id ON conversations(agent_id);
CREATE INDEX ix_conversations_created_at ON conversations(created_at);
```

**Entity:**
```csharp
public class Conversation
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? AgentId { get; set; }
    public string? RepositoryPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}
```

### 1.2 Message

```sql
CREATE TABLE messages (
    id UUID PRIMARY KEY,
    conversation_id UUID NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
    role VARCHAR(20) NOT NULL,  -- 'user', 'assistant', 'system'
    content TEXT NOT NULL,
    model VARCHAR(100),
    tokens_used INTEGER,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_messages_conversation_id ON messages(conversation_id);
CREATE INDEX ix_messages_created_at ON messages(created_at);
```

**Entity:**
```csharp
public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int? TokensUsed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Conversation? Conversation { get; set; }
}

public enum MessageRole { User, Assistant, System }
```

### 1.3 MessageRagContext

Stores RAG context used for a message (audit trail):

```sql
CREATE TABLE message_rag_contexts (
    id UUID PRIMARY KEY,
    message_id UUID NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    query TEXT NOT NULL,
    content_id VARCHAR(500) NOT NULL,
    chunk_index INTEGER NOT NULL,
    chunk_content TEXT NOT NULL,
    score DOUBLE PRECISION NOT NULL,
    source_path VARCHAR(1000),
    content_type VARCHAR(50),
    retrieved_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_message_rag_contexts_message_id ON message_rag_contexts(message_id);
CREATE INDEX ix_message_rag_contexts_content_id ON message_rag_contexts(content_id);
```

### 1.4 AgentExecution

Records agent invocations:

```sql
CREATE TABLE agent_executions (
    id UUID PRIMARY KEY,
    agent_id VARCHAR(100) NOT NULL,
    conversation_id UUID REFERENCES conversations(id) ON DELETE SET NULL,
    prompt TEXT NOT NULL,
    response TEXT,
    model VARCHAR(100),
    tokens_used INTEGER,
    duration_ms INTEGER,
    success BOOLEAN NOT NULL,
    error TEXT,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_agent_executions_agent_id ON agent_executions(agent_id);
CREATE INDEX ix_agent_executions_created_at ON agent_executions(created_at);
```

### 1.5 RagChunk

Vector embeddings for semantic search:

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE rag_chunks (
    id UUID PRIMARY KEY,
    content_id VARCHAR(500) NOT NULL,
    chunk_index INTEGER NOT NULL,
    content TEXT NOT NULL,
    embedding VECTOR(1536) NOT NULL,  -- OpenAI-compatible dimension
    source_path VARCHAR(1000),
    content_type VARCHAR(50),
    title VARCHAR(500),
    workspace_path VARCHAR(1000),
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL
);

-- HNSW index for fast similarity search
CREATE INDEX ix_rag_chunks_embedding ON rag_chunks 
    USING hnsw (embedding vector_cosine_ops);

CREATE INDEX ix_rag_chunks_content_id ON rag_chunks(content_id);
CREATE INDEX ix_rag_chunks_workspace_path ON rag_chunks(workspace_path);
CREATE INDEX ix_rag_chunks_content_type ON rag_chunks(content_type);
```

**Entity:**
```csharp
public class RagChunk
{
    public Guid Id { get; set; }
    public string ContentId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public Vector Embedding { get; set; }  // Pgvector.Vector
    public string? SourcePath { get; set; }
    public RagContentType ContentType { get; set; }
    public string? Title { get; set; }
    public string? WorkspacePath { get; set; }
    public string? Metadata { get; set; }  // JSON
    public DateTimeOffset CreatedAt { get; set; }
}
```

### 1.6 CodeNode

Code graph nodes:

```sql
CREATE TABLE code_nodes (
    id UUID PRIMARY KEY,
    name VARCHAR(500) NOT NULL,
    fully_qualified_name VARCHAR(1000) NOT NULL,
    node_type VARCHAR(50) NOT NULL,
    file_path VARCHAR(1000),
    start_line INTEGER,
    end_line INTEGER,
    signature TEXT,
    documentation TEXT,
    repository_path VARCHAR(1000),
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_code_nodes_name ON code_nodes(name);
CREATE INDEX ix_code_nodes_fqn ON code_nodes(fully_qualified_name);
CREATE INDEX ix_code_nodes_type ON code_nodes(node_type);
CREATE INDEX ix_code_nodes_repository ON code_nodes(repository_path);
```

**Entity:**
```csharp
public class CodeNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullyQualifiedName { get; set; } = string.Empty;
    public CodeNodeType NodeType { get; set; }
    public string? FilePath { get; set; }
    public int? StartLine { get; set; }
    public int? EndLine { get; set; }
    public string? Signature { get; set; }
    public string? Documentation { get; set; }
    public string? RepositoryPath { get; set; }
    public string? Metadata { get; set; }  // JSON
    public DateTimeOffset CreatedAt { get; set; }
}

public enum CodeNodeType
{
    Project, Namespace, Class, Interface, Struct, Record, Enum,
    Method, Property, Field, Event, Delegate, Constructor,
    Function, Module
}
```

### 1.7 CodeEdge

Code graph edges (relationships):

```sql
CREATE TABLE code_edges (
    id UUID PRIMARY KEY,
    source_node_id UUID NOT NULL REFERENCES code_nodes(id) ON DELETE CASCADE,
    target_node_id UUID NOT NULL REFERENCES code_nodes(id) ON DELETE CASCADE,
    edge_type VARCHAR(50) NOT NULL,
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_code_edges_source ON code_edges(source_node_id);
CREATE INDEX ix_code_edges_target ON code_edges(target_node_id);
CREATE INDEX ix_code_edges_type ON code_edges(edge_type);
```

**Entity:**
```csharp
public class CodeEdge
{
    public Guid Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid TargetNodeId { get; set; }
    public CodeEdgeType EdgeType { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CodeNode? SourceNode { get; set; }
    public CodeNode? TargetNode { get; set; }
}

public enum CodeEdgeType
{
    Contains, Implements, Inherits, Calls, References, Returns, Overrides
}
```

### 1.8 Workspace

Registered/onboarded workspaces:

```sql
CREATE TABLE workspaces (
    id VARCHAR(100) PRIMARY KEY,  -- Generated from path
    name VARCHAR(500) NOT NULL,
    path VARCHAR(1000) NOT NULL,
    is_git_repository BOOLEAN NOT NULL DEFAULT FALSE,
    current_commit_sha VARCHAR(40),
    rag_indexed_at TIMESTAMPTZ,
    rag_indexed_commit_sha VARCHAR(40),
    graph_indexed_at TIMESTAMPTZ,
    graph_indexed_commit_sha VARCHAR(40),
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX ix_workspaces_path ON workspaces(path);
```

**Entity:**
```csharp
public class Workspace
{
    public string Id { get; set; } = string.Empty;  // Generated hash
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsGitRepository { get; set; }
    public string? CurrentCommitSha { get; set; }
    public DateTimeOffset? RagIndexedAt { get; set; }
    public string? RagIndexedCommitSha { get; set; }
    public DateTimeOffset? GraphIndexedAt { get; set; }
    public string? GraphIndexedCommitSha { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

### 1.9 IndexMetadata

Tracks index freshness per file:

```sql
CREATE TABLE index_metadata (
    id UUID PRIMARY KEY,
    file_path VARCHAR(1000) NOT NULL,
    workspace_path VARCHAR(1000) NOT NULL,
    content_hash VARCHAR(64) NOT NULL,  -- SHA-256
    indexed_at TIMESTAMPTZ NOT NULL,
    index_type VARCHAR(50) NOT NULL  -- 'rag' or 'graph'
);

CREATE UNIQUE INDEX ix_index_metadata_file_workspace_type 
    ON index_metadata(file_path, workspace_path, index_type);
```

---

## 2. Developer Module Entities

### 2.1 Story

```sql
CREATE TABLE stories (
    id UUID PRIMARY KEY,
    title VARCHAR(500) NOT NULL,
    description TEXT,
    repository_path VARCHAR(1000),
    status VARCHAR(50) NOT NULL DEFAULT 'Created',
    worktree_path VARCHAR(1000),
    git_branch VARCHAR(500),
    analyzed_context JSONB,
    execution_plan JSONB,
    
    -- Issue integration
    issue_url VARCHAR(1000),
    issue_provider VARCHAR(50),
    issue_number INTEGER,
    issue_owner VARCHAR(100),
    issue_repo VARCHAR(100),
    
    -- Automation
    automation_mode VARCHAR(50) NOT NULL DEFAULT 'Assisted',
    dispatch_target VARCHAR(50) NOT NULL DEFAULT 'CopilotCli',
    
    -- Source
    source VARCHAR(50) NOT NULL DEFAULT 'User',
    source_guardian_id VARCHAR(100),
    
    -- Pattern
    pattern_name VARCHAR(100),
    pattern_language VARCHAR(50),
    priority VARCHAR(50) NOT NULL DEFAULT 'Medium',
    suggested_capability VARCHAR(100),
    
    -- Chat
    chat_history JSONB,
    
    -- Verification
    verification_passed BOOLEAN,
    verification_result JSONB,
    
    -- Orchestration
    current_wave INTEGER NOT NULL DEFAULT 0,
    gate_mode VARCHAR(50) NOT NULL DEFAULT 'AutoProceed',
    gate_result JSONB,
    max_parallelism INTEGER NOT NULL DEFAULT 4,
    
    -- PR
    pull_request_url VARCHAR(1000),
    
    -- Timestamps
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ
);

CREATE INDEX ix_stories_status ON stories(status);
CREATE INDEX ix_stories_repository ON stories(repository_path);
CREATE INDEX ix_stories_created_at ON stories(created_at);
```

### 2.2 StoryStep

```sql
CREATE TABLE story_steps (
    id UUID PRIMARY KEY,
    story_id UUID NOT NULL REFERENCES stories(id) ON DELETE CASCADE,
    "order" INTEGER NOT NULL,
    name VARCHAR(500) NOT NULL,
    capability VARCHAR(100) NOT NULL,
    description TEXT,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    assigned_agent_id VARCHAR(100),
    attempts INTEGER NOT NULL DEFAULT 0,
    output TEXT,
    error TEXT,
    approval VARCHAR(50),
    review_feedback TEXT,
    wave INTEGER NOT NULL DEFAULT 0,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

CREATE INDEX ix_story_steps_story_id ON story_steps(story_id);
CREATE INDEX ix_story_steps_status ON story_steps(status);
```

### 2.3 StoryTask (Optional - Subdivisions of Steps)

```sql
CREATE TABLE story_tasks (
    id UUID PRIMARY KEY,
    step_id UUID NOT NULL REFERENCES story_steps(id) ON DELETE CASCADE,
    "order" INTEGER NOT NULL,
    description TEXT NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    completed_at TIMESTAMPTZ
);

CREATE INDEX ix_story_tasks_step_id ON story_tasks(step_id);
```

---

## 3. Migration Strategy

### 3.1 EF Core Migrations

Each module has its own migration history:

```
src/Aura.Foundation/Data/Migrations/
    20251127_InitialFoundation.cs
    20251203_AddCodeGraph.cs
    20260113_AddWorkspaces.cs
    ...

src/Aura.Module.Developer/Migrations/
    20251128_InitialDeveloper.cs
    20251208_AddWorkflowSteps.cs
    20260115_AddPatternSupport.cs
    ...
```

### 3.2 Startup Migration

Migrations are applied automatically on startup:

```csharp
// In Program.cs
using var scope = app.Services.CreateScope();

var foundationDb = scope.ServiceProvider.GetRequiredService<AuraDbContext>();
await foundationDb.Database.MigrateAsync();

var developerDb = scope.ServiceProvider.GetRequiredService<DeveloperDbContext>();
await developerDb.Database.MigrateAsync();
```

### 3.3 pgvector Extension

Must be created before first migration:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

This is handled in the first migration that uses vectors.

---

## 4. Connection String

### 4.1 Configuration

```json
{
  "ConnectionStrings": {
    "auradb": "Host=127.0.0.1;Port=5433;Database=auradb;Username=postgres;Password=..."
  }
}
```

### 4.2 Aspire Integration

When using Aspire, the connection string is injected via environment variable:

```csharp
// In AppHost
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("auradb");

var api = builder.AddProject<Projects.Aura_Api>("api")
    .WithReference(postgres);
```

---

## 5. Vector Operations

### 5.1 Similarity Search

```csharp
var embedding = await _embeddingProvider.GenerateEmbeddingAsync(query);

var results = await _db.RagChunks
    .Where(c => c.WorkspacePath == workspacePath)
    .OrderBy(c => c.Embedding.CosineDistance(new Vector(embedding)))
    .Take(limit)
    .Select(c => new RagResult
    {
        ContentId = c.ContentId,
        ChunkIndex = c.ChunkIndex,
        Text = c.Content,
        Score = 1 - c.Embedding.CosineDistance(new Vector(embedding)),
        SourcePath = c.SourcePath,
        ContentType = c.ContentType
    })
    .ToListAsync();
```

### 5.2 Embedding Dimensions

Default: 1536 (OpenAI ada-002 compatible)

For Ollama with `nomic-embed-text`: 768 dimensions
- Schema should be flexible to accommodate different models

---

## 6. Workspace ID Generation

Deterministic ID from normalized path:

```csharp
public static string GenerateWorkspaceId(string path)
{
    var normalized = PathNormalizer.Normalize(path);
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(normalized));
    return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
}
```

---

## 7. JSON Storage Patterns

Several entities store JSON for flexibility:

| Column | Content |
|--------|---------|
| `analyzed_context` | RAG results, code graph context |
| `execution_plan` | Generated step plan from LLM |
| `chat_history` | Array of message objects |
| `verification_result` | Build/test/format results |
| `gate_result` | Quality gate check results |
| `metadata` | Flexible extension data |

**Querying JSON in PostgreSQL:**

```sql
-- Find stories with failed verification
SELECT * FROM stories 
WHERE verification_result->>'passed' = 'false';

-- Get specific metadata field
SELECT metadata->>'symbolName' FROM code_nodes WHERE ...;
```
