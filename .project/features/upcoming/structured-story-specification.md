# Structured Story Specification (Enrich v2)

**Status:** 📋 Design
**Priority:** High (core workflow quality)
**Created:** 2025-01-23

## Problem Statement

The current "Enrich" (Analyze) step in the Stories workflow produces free-form analysis that lacks the structure needed for:

1. **Reliable delegation** - Sub-agents can't parse what they need to do
2. **Acceptance criteria** - No clear definition of "done"
3. **Ambiguity resolution** - No mechanism to ask clarifying questions
4. **Progress tracking** - Can't measure completion against requirements

### Current Behavior

The `workflow-enrich.prompt` asks for:
- Core requirements
- Technical constraints
- Files likely to be affected
- Key considerations
- Suggested approach

Output is stored as free-form text in `workflow.AnalyzedContext`.

### Observed Issues

When testing "Add timeout configuration to LLM providers":
- Analysis is a narrative description, not structured spec
- No acceptance criteria defined
- No explicit sub-tasks that map to agent capabilities
- Plan step has to re-interpret the analysis

## Proposed Solution

### 1. Structured Specification Format

Replace free-form analysis with a JSON schema:

```json
{
  "specification": {
    "title": "string",
    "summary": "One-paragraph description of what will be built",
    "requirements": [
      {
        "id": "REQ-001",
        "description": "Add Timeout property to LlmProviderConfig",
        "type": "functional|non-functional|constraint",
        "acceptanceCriteria": [
          "Property exists with type TimeSpan",
          "Default value is 60 seconds",
          "Nullable to allow provider-specific defaults"
        ],
        "priority": "must-have|should-have|nice-to-have"
      }
    ],
    "technicalContext": {
      "languages": ["C#"],
      "frameworks": ["ASP.NET Core", "Azure.AI.OpenAI"],
      "patterns": ["Options pattern", "Dependency injection"],
      "affectedAreas": [
        {
          "path": "src/Aura.Foundation/Llm/",
          "reason": "LLM provider infrastructure"
        }
      ]
    },
    "subTasks": [
      {
        "id": "TASK-001",
        "title": "Add Timeout to LlmProviderConfig",
        "description": "Add TimeSpan? Timeout property with default 60s",
        "capability": "coding",
        "requirements": ["REQ-001"],
        "estimatedComplexity": "low|medium|high",
        "dependencies": []
      }
    ],
    "risks": [
      {
        "description": "Timeout too short may cause false failures",
        "mitigation": "Make configurable, document recommended values"
      }
    ],
    "outOfScope": [
      "Retry logic (separate feature)",
      "Circuit breaker pattern"
    ]
  },
  "questions": [
    {
      "id": "Q-001",
      "question": "Should timeout be per-provider or global?",
      "options": ["Per-provider", "Global with override", "Both"],
      "impact": "Affects config schema design"
    }
  ],
  "confidence": 0.85,
  "needsClarification": false
}
```

### 2. Interactive Clarification Mode

When `needsClarification: true` or `confidence < 0.7`:

1. API returns questions to the UI
2. UI presents questions to user
3. User answers are sent back to refine spec
4. Process repeats until `needsClarification: false`

### 3. Sub-Task to Agent Mapping

Each sub-task includes a `capability` field that maps to agent capabilities:

| Capability | Agent | Description |
|------------|-------|-------------|
| `coding` | coding-agent | Write/modify code |
| `testing` | coding-agent | Write unit tests |
| `documentation` | documentation-agent | Update docs |
| `review` | code-review-agent | Review changes |
| `research` | business-analyst-agent | Gather requirements |

### 4. Prompt Changes

New prompt: `workflow-enrich-v2.prompt`

```handlebars
---
description: Analyzes a development issue and produces structured specification
outputFormat: json
schema: specification-v1
tools:
  - file.list
  - file.read
  - search.grep
  - search.semantic
---
You are a technical analyst. Analyze this development task and produce a structured specification.

## Task
**Title:** {{title}}
**Description:** {{description}}

## Instructions

1. **Explore the codebase** using the provided tools to understand:
   - Existing patterns and conventions
   - Files that will need changes
   - Dependencies and constraints

2. **Identify requirements** - Break down the task into discrete requirements with acceptance criteria

3. **Decompose into sub-tasks** - Each sub-task should be:
   - Atomic (one agent can complete it)
   - Testable (has clear done criteria)
   - Mapped to a capability (coding, testing, documentation, review)

4. **Assess confidence** - If requirements are ambiguous, set `needsClarification: true` and include questions

5. **Output valid JSON** matching the specification schema

## Codebase Context
{{ragContext}}
```

### 5. API Changes

**`POST /api/developer/stories/{id}/analyze`**

Response changes:
```json
{
  "id": "guid",
  "status": "Analyzed|NeedsClarification",
  "specification": { ... },
  "questions": [ ... ]  // if needsClarification
}
```

**`POST /api/developer/stories/{id}/clarify`** (new endpoint)

Request:
```json
{
  "answers": [
    { "questionId": "Q-001", "answer": "Per-provider with global fallback" }
  ]
}
```

### 6. UI Changes

**Enrich Panel Updates:**

1. Show structured spec in readable format (not raw JSON)
2. If `needsClarification`, show questions with input fields
3. "Submit Answers" button to continue analysis
4. Visual confidence indicator
5. Editable requirements/sub-tasks before proceeding to Plan

## Implementation Plan

### Phase 1: Core Spec Format
1. Define JSON schema for specification
2. Create `workflow-enrich-v2.prompt`
3. Update `StoryService.AnalyzeAsync()` to parse structured output
4. Store spec in `AnalyzedContext` as structured JSON

### Phase 2: Interactive Clarification
1. Add `NeedsClarification` status
2. Create `/clarify` endpoint
3. Update UI to show questions and collect answers
4. Implement re-analysis with answers

### Phase 3: Plan Integration
1. Update `workflow-plan.prompt` to consume structured spec
2. Generate steps directly from sub-tasks
3. Map sub-task capabilities to agent selection

## Success Criteria

- [ ] Enrich produces valid JSON matching schema
- [ ] Each requirement has testable acceptance criteria
- [ ] Sub-tasks are atomic and capability-mapped
- [ ] Ambiguous requirements trigger clarification flow
- [ ] Plan step can parse and use structured spec
- [ ] UI displays spec in human-readable format

## Out of Scope (This Feature)

- Automatic sub-agent spawning (uses existing Plan→Execute flow)
- Requirement change tracking/versioning
- Collaborative editing of specs
- Integration with external issue trackers
