# MCP Tools Reference

**Last Updated**: 2026-01-27  
**Version**: 1.3.1

## Overview

This document provides a complete reference for all Aura MCP tools, including input schemas, output formats, and practical examples.

## Tool Categories

| Category | Tools | Purpose |
|----------|-------|---------|
| **Discovery** | `aura_search`, `aura_navigate`, `aura_inspect`, `aura_tree` | Find and explore code |
| **Transformation** | `aura_refactor`, `aura_generate`, `aura_edit` | Modify and create code |
| **Validation** | `aura_validate` | Check correctness |
| **Workflows** | `aura_workflow`, `aura_pattern` | Story-driven development |
| **Workspace** | `aura_workspace` | Manage workspace state |
| **Documentation** | `aura_docs` | Search documentation |

---

## Discovery Tools

### `aura_search`

**Purpose**: Semantic search across indexed codebase with similarity scoring.

**Category**: Read

#### Input Schema

```typescript
{
  query: string;              // Required: search query (concept, symbol, keyword)
  workspacePath?: string;     // Path to workspace/worktree (filters results)
  limit?: number;             // Max results (default: 10)
  contentType?: "code" | "docs" | "config" | "all";  // Filter by type
}
```

#### Output Format

```json
{
  "query": "authentication logic",
  "resultCount": 5,
  "results": [
    {
      "text": "public class AuthService...",
      "sourcePath": "src/Services/AuthService.cs",
      "score": 0.92,
      "contentType": "Code",
      "metadata": {
        "workspace": "C:\\work\\myrepo"
      }
    }
  ]
}
```

#### Examples

**Basic search**:
```javascript
aura_search({
  query: "authentication logic",
  workspacePath: "C:\\work\\myrepo"
})
```

**Search documentation only**:
```javascript
aura_search({
  query: "how to configure RAG",
  contentType: "docs",
  limit: 5
})
```

**Search for specific symbol**:
```javascript
aura_search({
  query: "IUserRepository implementation",
  workspacePath: "C:\\work\\myrepo",
  limit: 3
})
```

---

### `aura_navigate`

**Purpose**: Find code elements and their relationships (callers, implementations, usages, etc.).

**Category**: Read

#### Input Schema

```typescript
{
  operation: "callers" | "implementations" | "derived_types" | "usages" | 
             "by_attribute" | "extension_methods" | "by_return_type" | 
             "references" | "definition";
  
  // C# parameters
  symbolName?: string;         // Symbol to navigate from
  containingType?: string;     // Type containing the symbol (for disambiguation)
  solutionPath?: string;       // Path to .sln file (C# operations)
  attributeName?: string;      // For by_attribute operation
  targetType?: string;         // For extension_methods, by_return_type
  targetKind?: "method" | "class" | "property" | "all";  // For by_attribute
  
  // Python parameters
  filePath?: string;           // Path to file (Python operations)
  offset?: number;             // Character offset in file (Python)
  projectPath?: string;        // Project root (Python)
}
```

#### Output Format

**For callers, implementations, derived_types**:
```json
{
  "operation": "callers",
  "symbolName": "ProcessOrder",
  "results": [
    {
      "name": "OrderController.CreateOrder",
      "filePath": "src/Controllers/OrderController.cs",
      "line": 45,
      "kind": "Method",
      "containingType": "OrderController"
    }
  ]
}
```

**For usages, references**:
```json
{
  "operation": "usages",
  "symbolName": "AuthService",
  "results": [
    {
      "filePath": "src/Controllers/AuthController.cs",
      "line": 23,
      "snippet": "private readonly AuthService _authService;"
    }
  ]
}
```

#### Examples

**Find all callers of a method** (C#):
```javascript
aura_navigate({
  operation: "callers",
  symbolName: "ProcessOrder",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Find interface implementations** (C#):
```javascript
aura_navigate({
  operation: "implementations",
  symbolName: "IUserRepository",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Find usages of a symbol** (C#):
```javascript
aura_navigate({
  operation: "usages",
  symbolName: "UserService",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Find references** (Python):
```javascript
aura_navigate({
  operation: "references",
  filePath: "C:\\work\\myrepo\\src\\auth.py",
  offset: 1234,
  projectPath: "C:\\work\\myrepo"
})
```

**Find methods with specific attribute** (C#):
```javascript
aura_navigate({
  operation: "by_attribute",
  attributeName: "HttpGet",
  targetKind: "method",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

---

### `aura_inspect`

**Purpose**: Examine code structure (type members, class listings).

**Category**: Read

#### Input Schema

```typescript
{
  operation: "type_members" | "list_types";
  
  // For type_members
  typeName?: string;           // Type to inspect
  solutionPath?: string;       // Enables Roslyn fallback if graph empty
  
  // For list_types
  projectName?: string;        // Filter by project
  namespaceFilter?: string;    // Filter by namespace (partial match)
  nameFilter?: string;         // Filter by type name (partial match)
}
```

#### Output Format

**For type_members**:
```json
{
  "typeName": "UserService",
  "members": [
    {
      "name": "CreateUser",
      "kind": "Method",
      "returnType": "Task<User>",
      "parameters": ["CreateUserRequest request"],
      "accessModifier": "public"
    },
    {
      "name": "_repository",
      "kind": "Field",
      "type": "IUserRepository",
      "accessModifier": "private readonly"
    }
  ]
}
```

**For list_types**:
```json
{
  "types": [
    {
      "name": "UserService",
      "namespace": "MyApp.Services",
      "kind": "Class",
      "filePath": "src/Services/UserService.cs"
    },
    {
      "name": "IUserRepository",
      "namespace": "MyApp.Data",
      "kind": "Interface",
      "filePath": "src/Data/IUserRepository.cs"
    }
  ]
}
```

#### Examples

**Inspect class members**:
```javascript
aura_inspect({
  operation: "type_members",
  typeName: "UserService",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**List all types in a namespace**:
```javascript
aura_inspect({
  operation: "list_types",
  namespaceFilter: "MyApp.Services"
})
```

**Find types by name pattern**:
```javascript
aura_inspect({
  operation: "list_types",
  nameFilter: "Repository"
})
```

---

### `aura_tree`

**Purpose**: Get hierarchical tree view of codebase structure.

**Category**: Read

#### Input Schema

```typescript
{
  workspacePath: string;       // Required: workspace root
  pattern?: string;            // Filter pattern (default: '.' for all)
  maxDepth?: number;           // Max depth: 1=files, 2=files+types, 3=files+types+members (default: 2)
  detail?: "min" | "max";      // Level of detail
}
```

#### Output Format

```json
{
  "workspacePath": "C:\\work\\myrepo",
  "nodes": [
    {
      "id": "file:src/Services/UserService.cs",
      "name": "UserService.cs",
      "type": "file",
      "children": [
        {
          "id": "class:src/Services/UserService.cs:UserService",
          "name": "UserService",
          "type": "class",
          "children": [
            {
              "id": "method:src/Services/UserService.cs:UserService:CreateUser",
              "name": "CreateUser",
              "type": "method",
              "signature": "Task<User> CreateUser(CreateUserRequest)"
            }
          ]
        }
      ]
    }
  ]
}
```

#### Examples

**Get file and type structure**:
```javascript
aura_tree({
  workspacePath: "C:\\work\\myrepo",
  maxDepth: 2
})
```

**Get detailed structure with members**:
```javascript
aura_tree({
  workspacePath: "C:\\work\\myrepo",
  maxDepth: 3,
  detail: "max"
})
```

**Filter by pattern**:
```javascript
aura_tree({
  workspacePath: "C:\\work\\myrepo",
  pattern: "Services",
  maxDepth: 2
})
```

---

### `aura_get_node`

**Purpose**: Retrieve complete source code for a tree node.

**Category**: Read

#### Input Schema

```typescript
{
  workspacePath: string;       // Required: workspace root
  nodeId: string;              // Required: node ID from aura_tree
}
```

#### Output Format

```json
{
  "nodeId": "class:src/Services/UserService.cs:UserService",
  "content": "public class UserService\n{\n    ...\n}",
  "filePath": "src/Services/UserService.cs",
  "startLine": 10,
  "endLine": 45
}
```

#### Examples

**Get class source**:
```javascript
aura_get_node({
  workspacePath: "C:\\work\\myrepo",
  nodeId: "class:src/Services/UserService.cs:UserService"
})
```

---

## Transformation Tools

### `aura_refactor`

**Purpose**: Transform existing code (rename, extract, signature changes). Auto-detects language from filePath.

**Category**: Write

#### Input Schema

```typescript
{
  operation: "rename" | "change_signature" | "extract_interface" | 
             "extract_method" | "extract_variable" | "safe_delete" | 
             "move_type_to_file";
  
  // Common parameters
  symbolName?: string;         // Symbol to refactor
  newName?: string;            // New name for rename, extract operations
  containingType?: string;     // Type containing symbol (C# disambiguation)
  
  // C# parameters
  solutionPath?: string;       // Path to .sln file
  targetDirectory?: string;    // For move_type_to_file
  members?: string[];          // For extract_interface
  addParameters?: Array<{name: string, type: string, defaultValue?: string}>;
  removeParameters?: string[]; // For change_signature
  
  // Python parameters
  filePath?: string;           // Path to file
  projectPath?: string;        // Project root
  offset?: number;             // Character offset (for rename)
  startOffset?: number;        // Start offset (for extract operations)
  endOffset?: number;          // End offset (for extract operations)
  
  // Control flags
  analyze?: boolean;           // Default: true. Set to false to execute immediately.
  preview?: boolean;           // Return changes without applying (default: false)
  validate?: boolean;          // Run build after refactoring (default: false)
}
```

#### Output Format

**Analyze mode** (default):
```json
{
  "operation": "rename",
  "symbolName": "Workflow",
  "newName": "Story",
  "blastRadius": {
    "totalReferences": 64,
    "affectedFiles": 12,
    "relatedSymbols": ["WorkflowService", "IWorkflowRepository", "WorkflowDto"]
  },
  "suggestedOrder": [
    "Rename Workflow → Story",
    "Rename WorkflowService → StoryService",
    "Rename IWorkflowRepository → IStoryRepository"
  ],
  "executed": false
}
```

**Execute mode** (`analyze: false`):
```json
{
  "operation": "rename",
  "symbolName": "Workflow",
  "newName": "Story",
  "filesModified": 12,
  "referencesUpdated": 64,
  "success": true
}
```

#### Examples

**Rename symbol (analyze first)**:
```javascript
// Step 1: Analyze
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "C:\\work\\myrepo\\App.sln"
  // analyze: true (default)
})

// Step 2: Review blast radius, then execute
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  analyze: false
})
```

**Extract method** (C#):
```javascript
aura_refactor({
  operation: "extract_method",
  symbolName: "ProcessOrder",  // Method containing code to extract
  newName: "ValidateOrder",
  containingType: "OrderService",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Extract method** (Python):
```javascript
aura_refactor({
  operation: "extract_method",
  newName: "validate_user",
  filePath: "C:\\work\\myrepo\\src\\auth.py",
  startOffset: 1200,
  endOffset: 1450,
  projectPath: "C:\\work\\myrepo"
})
```

**Change method signature** (C#):
```javascript
aura_refactor({
  operation: "change_signature",
  symbolName: "CreateUser",
  containingType: "UserService",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  addParameters: [
    { name: "cancellationToken", type: "CancellationToken", defaultValue: "default" }
  ]
})
```

**Extract interface** (C#):
```javascript
aura_refactor({
  operation: "extract_interface",
  symbolName: "UserService",
  newName: "IUserService",
  members: ["CreateUser", "GetUser", "UpdateUser"],
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

---

### `aura_generate`

**Purpose**: Create new code elements (types, methods, properties, tests).

**Category**: Write

#### Input Schema

```typescript
{
  operation: "implement_interface" | "constructor" | "property" | 
             "method" | "create_type" | "tests";
  
  // Common
  solutionPath: string;        // Required: path to .sln
  className?: string;          // Target class (for existing class operations)
  
  // create_type parameters
  typeName?: string;           // Name of new type
  typeKind?: "class" | "interface" | "record" | "struct";
  targetDirectory?: string;    // Directory for new file
  baseClass?: string;          // Base class to inherit
  implements?: string[];       // Interfaces to implement
  isSealed?: boolean;          // Sealed class
  isAbstract?: boolean;        // Abstract class
  isStatic?: boolean;          // Static type or method
  documentationSummary?: string;  // XML doc summary
  primaryConstructorParameters?: Array<{name: string, type: string, defaultValue?: string}>;
  typeParameters?: Array<{name: string, constraints?: string[]}>;  // Generics
  
  // tests parameters
  target?: string;             // Class name, Class.Method, or namespace
  count?: number;              // Explicit test count
  maxTests?: number;           // Max tests (default: 20)
  focus?: "all" | "happy_path" | "edge_cases" | "error_handling";
  testFramework?: "xunit" | "nunit" | "mstest";
  outputDirectory?: string;    // Test file location
  analyzeOnly?: boolean;       // Return analysis without generating
  validateCompilation?: boolean;  // Validate code compiles
  
  // method parameters
  methodName?: string;         // Method name
  returnType?: string;         // Return type
  parameters?: Array<{name: string, type: string, defaultValue?: string}>;
  isAsync?: boolean;           // Async method
  isExtension?: boolean;       // Extension method
  methodModifier?: "virtual" | "override" | "abstract" | "sealed" | "new";
  body?: string;               // Optional method body
  testAttribute?: string;      // For test methods: Fact, Test, TestMethod
  
  // property parameters
  propertyName?: string;       // Property name
  propertyType?: string;       // Property type
  hasGetter?: boolean;         // Include getter (default: true)
  hasSetter?: boolean;         // Include setter (default: true)
  hasInit?: boolean;           // Use init instead of set (C# 9+)
  isRequired?: boolean;        // Add required modifier (C# 11+)
  initialValue?: string;       // Initial value
  isField?: boolean;           // Generate field instead
  isReadonly?: boolean;        // Readonly field
  
  // Common modifiers
  accessModifier?: string;     // e.g., "public", "private", "protected"
  documentation?: string;      // XML doc summary
  attributes?: Array<{name: string, arguments?: string[]}>;
  
  // implement_interface
  interfaceName?: string;      // Interface to implement
  explicitImplementation?: boolean;  // Explicit implementation
  
  // constructor
  members?: string[];          // Fields/properties to initialize
  
  // Control
  preview?: boolean;           // Return without applying (default: false)
}
```

#### Output Format

**For create_type, method, property**:
```json
{
  "operation": "create_type",
  "typeName": "UserService",
  "filePath": "src/Services/UserService.cs",
  "success": true,
  "generatedCode": "public class UserService : IUserService\n{\n    ...\n}"
}
```

**For tests** (analyze mode):
```json
{
  "operation": "tests",
  "target": "UserService",
  "analyzeOnly": true,
  "testableMethodsCount": 8,
  "testableMethodsfilePath": [
    "CreateUser(CreateUserRequest): Task<User>",
    "GetUser(Guid): Task<User>",
    "UpdateUser(Guid, UpdateUserRequest): Task<User>"
  ],
  "recommendedTestCount": 15
}
```

**For tests** (execute mode):
```json
{
  "operation": "tests",
  "target": "UserService",
  "testsGenerated": 15,
  "testFilePath": "tests/Services/UserServiceTests.cs",
  "compilesSuccessfully": true,
  "framework": "xunit"
}
```

#### Examples

**Create new class**:
```javascript
aura_generate({
  operation: "create_type",
  typeName: "OrderService",
  typeKind: "class",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  targetDirectory: "src/Services",
  implements: ["IOrderService"],
  documentationSummary: "Handles order processing logic"
})
```

**Create record with primary constructor** (C# 9+):
```javascript
aura_generate({
  operation: "create_type",
  typeName: "CreateUserRequest",
  typeKind: "record",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  primaryConstructorParameters: [
    { name: "Email", type: "string" },
    { name: "FirstName", type: "string" },
    { name: "LastName", type: "string" }
  ]
})
```

**Add method to existing class**:
```javascript
aura_generate({
  operation: "method",
  className: "UserService",
  methodName: "DeleteUser",
  returnType: "Task<bool>",
  parameters: [
    { name: "userId", type: "Guid" }
  ],
  isAsync: true,
  solutionPath: "C:\\work\\myrepo\\App.sln",
  documentation: "Deletes a user by ID"
})
```

**Add property with init accessor** (C# 9+):
```javascript
aura_generate({
  operation: "property",
  className: "User",
  propertyName: "Email",
  propertyType: "string",
  hasGetter: true,
  hasInit: true,
  isRequired: true,
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Generate unit tests** (analyze first):
```javascript
// Step 1: Analyze
aura_generate({
  operation: "tests",
  target: "UserService",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  analyzeOnly: true
})

// Step 2: Generate
aura_generate({
  operation: "tests",
  target: "UserService",
  solutionPath: "C:\\work\\myrepo\\App.sln",
  focus: "all",
  validateCompilation: true,
  outputDirectory: "Services"
})
```

**Implement interface**:
```javascript
aura_generate({
  operation: "implement_interface",
  className: "UserService",
  interfaceName: "IUserService",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

---

### `aura_edit`

**Purpose**: Surgical line-based text editing (insert, replace, delete lines).

**Category**: Write

#### Input Schema

```typescript
{
  operation: "insert_lines" | "replace_lines" | "delete_lines" | "append" | "prepend";
  filePath: string;            // Required: absolute file path
  line?: number;               // For insert_lines: insert AFTER this line (use 0 for start)
  startLine?: number;          // For replace_lines, delete_lines (1-based, inclusive)
  endLine?: number;            // For replace_lines, delete_lines (1-based, inclusive)
  content?: string;            // Content to insert/replace (use \n for newlines)
  preview?: boolean;           // Return without writing (default: false)
}
```

#### Output Format

```json
{
  "operation": "insert_lines",
  "filePath": "C:\\work\\myrepo\\config.json",
  "linesModified": 3,
  "success": true
}
```

#### Examples

**Insert lines after line 10**:
```javascript
aura_edit({
  operation: "insert_lines",
  filePath: "C:\\work\\myrepo\\src\\config.json",
  line: 10,
  content: '  "newSetting": true,\n  "anotherSetting": "value"'
})
```

**Replace lines 5-8**:
```javascript
aura_edit({
  operation: "replace_lines",
  filePath: "C:\\work\\myrepo\\README.md",
  startLine: 5,
  endLine: 8,
  content: "## New Section\n\nUpdated content here."
})
```

**Append to end of file**:
```javascript
aura_edit({
  operation: "append",
  filePath: "C:\\work\\myrepo\\.gitignore",
  content: "\n*.tmp\n*.log"
})
```

---

## Validation Tools

### `aura_validate`

**Purpose**: Check code correctness (compilation, tests).

**Category**: Read

#### Input Schema

```typescript
{
  operation: "compilation" | "tests";
  
  // For compilation
  solutionPath?: string;       // Path to .sln
  projectName?: string;        // Specific project to build
  includeWarnings?: boolean;   // Include warnings (default: false)
  
  // For tests
  projectPath?: string;        // Path to test project
  filter?: string;             // Test filter (dotnet test --filter syntax)
  timeoutSeconds?: number;     // Timeout (default: 120)
}
```

#### Output Format

**For compilation** (success):
```json
{
  "operation": "compilation",
  "success": true,
  "buildTime": "3.2s",
  "warnings": 0,
  "errors": 0
}
```

**For compilation** (errors):
```json
{
  "operation": "compilation",
  "success": false,
  "errors": [
    {
      "file": "src/Services/UserService.cs",
      "line": 45,
      "code": "CS0246",
      "message": "The type or namespace name 'User' could not be found"
    }
  ]
}
```

**For tests**:
```json
{
  "operation": "tests",
  "success": true,
  "totalTests": 247,
  "passed": 247,
  "failed": 0,
  "skipped": 0,
  "duration": "12.5s"
}
```

#### Examples

**Validate compilation**:
```javascript
aura_validate({
  operation: "compilation",
  solutionPath: "C:\\work\\myrepo\\App.sln"
})
```

**Run specific tests**:
```javascript
aura_validate({
  operation: "tests",
  projectPath: "C:\\work\\myrepo\\tests\\MyApp.Tests",
  filter: "FullyQualifiedName~UserServiceTests"
})
```

---

## Workflow Tools

### `aura_workflow`

**Purpose**: Manage development workflows/stories.

**Category**: CRUD

#### Input Schema

```typescript
{
  operation: "list" | "get" | "get_by_path" | "create" | "enrich" | "update_step" | "complete";
  
  // For get
  storyId?: string;            // Story GUID
  
  // For get_by_path
  workspacePath?: string;      // Auto-discover current story
  
  // For create
  issueUrl?: string;           // GitHub issue URL
  repositoryPath?: string;     // Local repo path for worktree
  
  // For enrich
  pattern?: string;            // Pattern name to apply
  language?: string;           // Language overlay (e.g., "csharp", "python")
  steps?: Array<{
    name: string;
    capability: string;
    description?: string;
    input?: object;
  }>;
  
  // For update_step
  stepId?: string;             // Step GUID
  status?: "completed" | "failed" | "skipped" | "pending";
  output?: string;             // Step result
  error?: string;              // Error message (for failed)
  skipReason?: string;         // Reason (for skipped)
}
```

#### Output Format

**For list**:
```json
{
  "stories": [
    {
      "id": "abc123-...",
      "title": "Implement user authentication",
      "status": "in_progress",
      "repositoryPath": "C:\\work\\myrepo",
      "branchName": "workflow/abc123",
      "pattern": "implement-feature",
      "language": "csharp"
    }
  ]
}
```

**For get**:
```json
{
  "id": "abc123-...",
  "title": "Implement user authentication",
  "description": "Add JWT-based auth...",
  "status": "in_progress",
  "steps": [
    {
      "id": "step-1",
      "name": "Create AuthService",
      "capability": "aura_generate",
      "status": "completed",
      "output": "AuthService created successfully"
    }
  ],
  "pattern": "implement-feature",
  "patternContent": "# Implement Feature Pattern\n\n1. Analyze requirements..."
}
```

#### Examples

**List all stories**:
```javascript
aura_workflow({
  operation: "list"
})
```

**Get story by worktree path** (auto-discovery):
```javascript
aura_workflow({
  operation: "get_by_path",
  workspacePath: "C:\\work\\myrepo\\.worktrees\\abc123"
})
```

**Create story from GitHub issue**:
```javascript
aura_workflow({
  operation: "create",
  issueUrl: "https://github.com/org/repo/issues/123",
  repositoryPath: "C:\\work\\myrepo"
})
```

**Enrich with pattern**:
```javascript
aura_workflow({
  operation: "enrich",
  storyId: "abc123-...",
  pattern: "comprehensive-rename",
  language: "csharp"
})
```

**Complete story** (squash, push, create PR):
```javascript
aura_workflow({
  operation: "complete",
  storyId: "abc123-..."
})
```

---

### `aura_pattern`

**Purpose**: Load operational patterns (step-by-step playbooks).

**Category**: Read

#### Input Schema

```typescript
{
  operation: "list" | "get";
  name?: string;               // Pattern name (without .md)
  language?: string;           // Language overlay (e.g., "csharp", "python")
}
```

#### Output Format

**For list**:
```json
{
  "patterns": [
    {
      "name": "comprehensive-rename",
      "title": "Comprehensive Rename Pattern",
      "description": "Rename symbols across codebase with blast radius analysis",
      "overlays": ["csharp", "python", "typescript"]
    }
  ]
}
```

**For get**:
```json
{
  "name": "comprehensive-rename",
  "content": "# Comprehensive Rename\n\n## Phase 1: Analysis\n...",
  "language": "csharp",
  "overlayContent": "## C#-Specific Steps\n..."
}
```

#### Examples

**List available patterns**:
```javascript
aura_pattern({
  operation: "list"
})
```

**Load pattern with C# overlay**:
```javascript
aura_pattern({
  operation: "get",
  name: "comprehensive-rename",
  language: "csharp"
})
```

---

## Workspace Tools

### `aura_workspace`

**Purpose**: Manage workspace state (worktree detection, cache invalidation).

**Category**: Read/Write

#### Input Schema

```typescript
{
  operation: "detect_worktree" | "invalidate_cache" | "status";
  path: string;                // Required: path to workspace/solution/worktree
}
```

#### Output Format

```json
{
  "operation": "detect_worktree",
  "path": "C:\\work\\myrepo\\.worktrees\\abc123",
  "isWorktree": true,
  "mainRepoPath": "C:\\work\\myrepo",
  "branch": "workflow/abc123"
}
```

#### Examples

**Detect if path is a worktree**:
```javascript
aura_workspace({
  operation: "detect_worktree",
  path: "C:\\work\\myrepo\\.worktrees\\abc123"
})
```

**Invalidate workspace cache**:
```javascript
aura_workspace({
  operation: "invalidate_cache",
  path: "C:\\work\\myrepo"
})
```

---

## Documentation Tools

### `aura_docs`

**Purpose**: Search Aura documentation with semantic retrieval.

**Category**: Read

#### Input Schema

```typescript
{
  query: string;               // Required: documentation search query
}
```

#### Output Format

```json
{
  "query": "how to configure RAG",
  "resultCount": 5,
  "results": [
    {
      "content": "## RAG Configuration\n\nWorkspaces are indexed...",
      "sourcePath": "src/Aura.Api/Docs/configuration.md",
      "score": 0.88,
      "contentType": "Documentation",
      "metadata": {}
    }
  ]
}
```

#### Examples

**Search documentation**:
```javascript
aura_docs({
  query: "how to configure Azure OpenAI"
})
```

---

## Common Patterns

### 1. **Comprehensive Rename Workflow**

```javascript
// Step 1: Search for occurrences
aura_search({ query: "Workflow", workspacePath: "..." })

// Step 2: Analyze blast radius
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "...",
  analyze: true  // Default
})

// Step 3: Review, then execute
aura_refactor({
  operation: "rename",
  symbolName: "Workflow",
  newName: "Story",
  solutionPath: "...",
  analyze: false
})

// Step 4: Validate
aura_validate({ operation: "compilation", solutionPath: "..." })
```

### 2. **Feature Implementation with Tests**

```javascript
// Step 1: Create service
aura_generate({
  operation: "create_type",
  typeName: "OrderService",
  implements: ["IOrderService"],
  solutionPath: "..."
})

// Step 2: Generate tests
aura_generate({
  operation: "tests",
  target: "OrderService",
  solutionPath: "...",
  validateCompilation: true
})

// Step 3: Run tests
aura_validate({
  operation: "tests",
  filter: "OrderServiceTests"
})
```

### 3. **Story-Driven Development**

```javascript
// Step 1: Create from GitHub issue
aura_workflow({
  operation: "create",
  issueUrl: "https://github.com/org/repo/issues/123",
  repositoryPath: "C:\\work\\myrepo"
})

// Step 2: Enrich with pattern
aura_workflow({
  operation: "enrich",
  storyId: "{guid}",
  pattern: "implement-feature",
  language: "csharp"
})

// Steps 3-N: Execute via UI or API

// Final: Complete story
aura_workflow({ operation: "complete", storyId: "{guid}" })
```

---

## Next Steps

- **Agent Integration**: See [agents.md](agents.md) for workflows and best practices
- **Configuration**: See [configuration.md](configuration.md) for LLM and RAG settings
- **Troubleshooting**: See [troubleshooting.md](troubleshooting.md) for common issues
