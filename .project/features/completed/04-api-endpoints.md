# API Endpoints Specification

**Version:** 1.0  
**Status:** ✅ Complete  
**Last Updated:** 2025-12-12

## Overview

The Aura API is a REST API for managing agents, workflows, and development interactions. It follows RESTful conventions with JSON payloads.

## Base URL

```
http://localhost:5258/api
```

## Endpoints

### Agents

#### List Agents

```http
GET /api/agents
```

**Response:**
```json
{
  "agents": [
    {
      "id": "roslyn-agent",
      "name": "Roslyn C# Agent",
      "capabilities": ["coding", "csharp-coding"],
      "priority": 60,
      "provider": "ollama",
      "model": "qwen2.5-coder:7b",
      "description": "C# code generation with Roslyn validation"
    }
  ]
}
```

#### Get Agent

```http
GET /api/agents/{id}
```

#### Register Agent (Runtime)

```http
POST /api/agents
Content-Type: application/json

{
  "name": "Custom Agent",
  "capabilities": ["coding", "python"],
  "priority": 55,
  "provider": "ollama",
  "model": "codellama:7b",
  "systemPrompt": "You are an expert Python developer..."
}
```

**Response:** `201 Created` with agent details

#### Unregister Agent

```http
DELETE /api/agents/{id}
```

**Response:** `204 No Content`

#### Get Agents by Capability

```http
GET /api/agents?capability=csharp-coding
```

Returns agents sorted by priority (lowest first).

---

### Workflows

#### List Workflows

```http
GET /api/workflows
GET /api/workflows?status=Planned
```

**Response:**
```json
{
  "workflows": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "workItemId": "github:owner/repo#123",
      "workItemTitle": "Add user authentication",
      "status": "Planned",
      "stepCount": 3,
      "createdAt": "2025-11-26T10:00:00Z"
    }
  ]
}
```

#### Get Workflow

```http
GET /api/workflows/{id}
```

**Response:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "workItemId": "github:owner/repo#123",
  "workItemTitle": "Add user authentication",
  "workItemDescription": "As a user, I want to log in...",
  "status": "Planned",
  "workspacePath": "/workspaces/repo-wt-123",
  "gitBranch": "feature/issue-123",
  "EnrichedContext": { ... },
  "steps": [
    {
      "id": "...",
      "order": 1,
      "name": "Implement AuthService",
      "capability": "csharp-coding",
      "status": "Pending"
    }
  ],
  "createdAt": "2025-11-26T10:00:00Z",
  "updatedAt": "2025-11-26T10:05:00Z"
}
```

#### Create Workflow

```http
POST /api/workflows
Content-Type: application/json

{
  "workItemId": "github:owner/repo#123",
  "workItemTitle": "Add user authentication",
  "workItemDescription": "As a user, I want to log in...",
  "workspacePath": "/path/to/repo"
}
```

**Response:** `201 Created` with workflow details

#### Delete Workflow

```http
DELETE /api/workflows/{id}
```

---

### Workflow Phases

The three-phase workflow model: ENRICH → Plan → Execute

#### Phase 1: ENRICH

Augment the workflow with codebase context via RAG.

```http
POST /api/workflows/{id}/enrich
```

**Response:**
```json
{
  "status": "Enriched",
  "context": {
    "relevantFiles": ["src/Services/UserService.cs", "src/Models/User.cs"],
    "architectureNotes": "Uses repository pattern...",
    "existingPatterns": ["Dependency injection", "async/await"]
  }
}
```

#### Phase 2: Plan

Break the workflow into executable steps.

```http
POST /api/workflows/{id}/plan
```

**Response:**
```json
{
  "status": "Planned",
  "steps": [
    {
      "id": "...",
      "order": 1,
      "name": "Implement AuthService",
      "capability": "csharp-coding",
      "description": "Create authentication service with JWT support"
    },
    {
      "id": "...",
      "order": 2,
      "name": "Write AuthService tests",
      "capability": "testing",
      "description": "Unit tests for AuthService"
    }
  ]
}
```

#### Phase 2b: Replan

Replan with user feedback.

```http
POST /api/workflows/{id}/replan
Content-Type: application/json

{
  "feedback": "Also add password reset functionality"
}
```

---

### Step Execution

#### Execute Step

Execute a single step with optional agent override.

```http
POST /api/workflows/{id}/steps/{stepId}/execute
Content-Type: application/json

{
  "agentId": "roslyn-agent"  // Optional override
}
```

**Response:**
```json
{
  "stepId": "...",
  "status": "Completed",
  "agentId": "roslyn-agent",
  "output": {
    "files": [
      {
        "path": "src/Services/AuthService.cs",
        "content": "public class AuthService..."
      }
    ],
    "summary": "Created AuthService with Login and Logout methods"
  },
  "durationMs": 5432
}
```

#### Get Step Output

```http
GET /api/workflows/{id}/steps/{stepId}/output
```

#### Retry Step

```http
POST /api/workflows/{id}/steps/{stepId}/retry
Content-Type: application/json

{
  "feedback": "Use BCrypt instead of SHA256 for password hashing"
}
```

#### Skip Step

```http
POST /api/workflows/{id}/steps/{stepId}/skip
```

---

### Chat (Augmented Development)

Interactive chat within workflow context.

#### Send Message

```http
POST /api/workflows/{id}/chat
Content-Type: application/json

{
  "message": "Add rate limiting to the AuthService"
}
```

**Response:**
```json
{
  "response": "I'll add rate limiting. Here's my plan:\n1. Add RateLimiter middleware...",
  "planUpdated": true,
  "newSteps": [
    {
      "order": 3,
      "name": "Add rate limiting middleware",
      "capability": "csharp-coding"
    }
  ]
}
```

#### Get Chat History

```http
GET /api/workflows/{id}/chat
```

**Response:**
```json
{
  "messages": [
    { "role": "user", "content": "Add rate limiting...", "createdAt": "..." },
    { "role": "assistant", "content": "I'll add...", "createdAt": "..." }
  ]
}
```

---

### Real-time Updates (SSE)

Server-Sent Events for workflow updates.

```http
GET /api/workflows/{id}/events
Accept: text/event-stream
```

**Events:**
```
event: step.started
data: {"stepId": "...", "agentId": "roslyn-agent"}

event: step.progress
data: {"stepId": "...", "message": "Generating code..."}

event: step.completed
data: {"stepId": "...", "status": "Completed"}

event: workflow.updated
data: {"status": "Executing", "currentStep": 2}
```

---

### Issues (External Sync)

Sync with external issue trackers.

#### Sync Issues

```http
POST /api/issues/sync
Content-Type: application/json

{
  "provider": "github",
  "repository": "owner/repo"
}
```

#### Import Issue as Workflow

```http
POST /api/issues/{issueId}/import
Content-Type: application/json

{
  "workspacePath": "/path/to/repo"
}
```

**Response:** Created workflow

---

### Health

```http
GET /health          # Basic health
GET /health/ready    # Ready to serve
GET /health/live     # Liveness probe
```

---

## Error Responses

All errors follow this format:

```json
{
  "error": {
    "code": "WORKFLOW_NOT_FOUND",
    "message": "Workflow with id '...' not found",
    "details": { ... }
  }
}
```

| HTTP Status | When |
|-------------|------|
| 400 | Bad request (validation error) |
| 404 | Resource not found |
| 409 | Conflict (e.g., step already running) |
| 500 | Internal server error |

## Rate Limiting

No rate limiting in v1 (local-only deployment).

## Authentication

No authentication in v1 (local-only deployment).

## OpenAPI

Available at:
```
GET /swagger
GET /openapi/v1.json
```
