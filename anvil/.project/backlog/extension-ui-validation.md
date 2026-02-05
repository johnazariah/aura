# Backlog: Extension UI Validation

**Capability:** 6 - Validate the VS Code Extension user experience  
**Priority:** Medium - Important but distinct from agent testing

## Functional Requirements

### Render Validation
- Extension loads without errors
- All views render correctly (workflows, chat, settings)
- No console errors or unhandled exceptions

### Functional Validation
- Buttons do what their labels say
- State is displayed correctly (workflow status, step progress)
- Navigation between views works
- Data refreshes when expected

### Ceremony Detection
- Track click depth to accomplish common tasks
- Flag workflows that require too many interactions
- Measure time-to-task-completion for key scenarios

### Common Task Scenarios
- View list of workflows
- Create a new workflow
- View workflow details and steps
- Start/stop workflow execution
- Chat with an agent about code

### Accessibility Baseline
- Keyboard navigation works
- Screen reader compatibility (basic)
- Sufficient contrast and readability

## Open Questions (for Research)

- What framework for VS Code extension UI testing?
- How to define "too many clicks" threshold?
- How to automate click-path recording for new features?
