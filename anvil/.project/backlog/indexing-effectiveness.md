# Backlog: Indexing System Effectiveness

**Capability:** 5 - Validate that agents use the index instead of guessing  
**Priority:** High - Core value of Aura's RAG + Code Graph

## Functional Requirements

### Index Usage Detection
- Detect when agents query the semantic index vs. grep/file scanning
- Identify "guessing" patterns: reading many files sequentially looking for something
- Identify "knowing" patterns: directly navigating to relevant code

### Context Quality
- Agent cites indexed context in reasoning (not just file paths)
- Agent finds relevant code without exhaustive scanning
- Cross-references are followed through the code graph

### Anti-Patterns to Detect
- Excessive file.list/file.read sequences = fishing expedition
- grep_search for things the index should know = not using index
- Reading entire files when only a symbol was needed = inefficient

### Effectiveness Metrics
- "First-try" accuracy: did agent find the right thing immediately?
- Context relevance: was cited context actually useful?
- Navigation efficiency: steps to find relevant code

## Open Questions (for Research)

- How to capture agent reasoning to detect "guessing"?
- What's a reasonable baseline for index usage?
- How to test index quality independent of agent behavior?
