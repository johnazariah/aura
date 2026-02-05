# Backlog: MCP Tool Effectiveness

**Capability:** 4 - Validate that Aura MCP tools are actually used  
**Priority:** High - Core differentiator of Aura

## Functional Requirements

### Tool Usage Detection
- During story execution, detect which MCP tools were called
- Distinguish between Aura tools (aura_search, aura_generate, etc.) and basic tools (file.read, grep)
- Flag scenarios where Aura tools were available but not used

### Effectiveness Metrics
- Ratio of Aura tool calls to basic file operations
- Which tools are being used vs. ignored
- Correlation between tool usage and outcome quality

### Improvement Detection
- When a new MCP tool is added, does the LLM start using it?
- When a tool is improved, does usage pattern change?
- Track tool adoption over time

### Failure Modes
- Tools available but ignored → flag for investigation
- Tools called but failed → capture error details
- Tools called with wrong parameters → log for tool improvement

## Open Questions (for Research)

- How to capture tool call logs from Copilot CLI execution?
- What's the right threshold for "not using tools enough"?
- How to attribute success/failure to tool usage vs. other factors?
