# Backlog: Story Sophistication Ladder

**Capability:** Cross-cutting - Test scenarios at increasing complexity  
**Priority:** Medium - Grows alongside Aura capabilities

## Functional Requirements

### Complexity Levels

#### Level 1: Simple Greenfield
- Single file creation
- No dependencies on existing code
- Example: "Create Hello World console app"

#### Level 2: Single File Feature
- Modify or extend one existing file
- Requires understanding file context
- Example: "Add a method that validates email format"

#### Level 3: Multi-File Feature
- Create or modify multiple coordinated files
- May include tests alongside implementation
- Example: "Add an API endpoint with controller, service, and tests"

#### Level 4: Cross-Cutting Change
- Modify pattern across many files
- Requires finding all relevant locations
- Example: "Add logging to all service methods"

#### Level 5: Pattern-Following
- Create new component following existing patterns
- Requires understanding and replicating architecture
- Example: "Create a new microservice like the existing OrderService"

### Scenario Curation
- At least 2-3 scenarios per complexity level
- Scenarios should be deterministic in expected outcome
- Scenarios should cover different languages/frameworks over time

### Progression Tracking
- Track which levels Aura handles reliably
- Detect regressions at specific complexity levels
- Guide development priorities based on capability gaps

## Open Questions (for Research)

- How to create reproducible scenarios at higher complexity?
- How to validate "pattern following" without structural comparison?
- Should levels map to different test repositories?
