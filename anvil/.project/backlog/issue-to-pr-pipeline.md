# Backlog: Issue-to-PR Pipeline

**Capability:** 3 - GitHub Issue to PR  
**Priority:** Medium - Extends story execution to real-world source

## Functional Requirements

### Issue Discovery
- Connect to dedicated test GitHub account
- Discover issues tagged for Anvil testing
- Parse issue body as story definition

### Issue Suite Management
- Curated set of test issues at various complexity levels
- Issues represent known-good test cases
- Ability to reset issues to clean state between runs

### PR Creation & Validation
- Story execution produces a PR (not just local code)
- PR is linked to the originating issue
- PR passes CI checks (if configured)

### Test Account Isolation
- Separate from production GitHub usage
- Dedicated repos for Anvil testing
- No risk of polluting real projects

## Open Questions (for Research)

- Should Anvil create issues, or only consume pre-created ones?
- How to clean up PRs/branches between runs?
- How to simulate different issue formats (terse, verbose, ambiguous)?
