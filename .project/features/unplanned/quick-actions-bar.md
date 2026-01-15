# Quick Actions Bar

**Status:** 📋 Backlog  
**Priority:** Low (Polish)  
**Parent:** [Story Chat](../completed/story-chat.md)  
**Estimated Effort:** 2-4 hours

## Overview

Add a Quick Actions bar to the workflow panel with common operations as buttons. This is a UX polish item - users can already type these commands in chat.

## Design

```
┌─────────────────────────────────────────────────────────────────┐
│  Quick Actions                                                  │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │ 🔨 Build│  │ 🧪 Test │  │ 📝 Commit│  │ 🚀 PR   │           │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘           │
│                                                                 │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │ 📊 Status│  │ ↩️ Undo │  │ 📋 Plan │  │ ✅ Done │           │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘           │
└─────────────────────────────────────────────────────────────────┘
```

## Button Actions

Each button just sends a pre-defined message to the chat:

| Button | Message Sent |
|--------|--------------|
| Build | "Build the project and show any errors" |
| Test | "Run the tests and summarize results" |
| Commit | "Commit the current changes with an appropriate message" |
| PR | "Push the branch and create a pull request" |
| Status | "Show git status and what's changed" |
| Undo | "Revert the last file changes you made" |
| Plan | "Outline the remaining work as a plan" |
| Done | "We're done - summarize what was accomplished and close the issue" |

## Implementation

Add to `workflowPanelProvider.ts` in the chat section:

```typescript
<div class="quick-actions-bar">
    <button class="quick-action" onclick="sendQuickAction('Build the project')">🔨 Build</button>
    <button class="quick-action" onclick="sendQuickAction('Run the tests')">🧪 Test</button>
    <button class="quick-action" onclick="sendQuickAction('Commit the changes')">📝 Commit</button>
    <button class="quick-action" onclick="sendQuickAction('Create a PR')">🚀 PR</button>
</div>

<script>
function sendQuickAction(message) {
    document.getElementById('chatInput').value = message;
    sendChat();
}
</script>
```

## Success Criteria

- [ ] Quick action buttons appear below chat input
- [ ] Clicking a button sends the message to chat
- [ ] Buttons are disabled when workflow is completed/cancelled
