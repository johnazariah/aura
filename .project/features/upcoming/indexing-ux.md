# Indexing UX & Frontend

**Status:** Proposed  
**Priority:** Medium  
**Estimated Effort:** 4-5 hours  
**Created:** 2025-12-12  
**Consolidates:** indexing-ux-improvements.md, indexing-progress-ui.md

## Overview

User experience improvements for the indexing pipeline, including progress visibility, automatic triggering, and query UI.

## Features

### 1. Recursive Solution Discovery

**Status:** 🔲 Not Started  
**Effort:** 1 hour

**Problem:** `findSolutionPath` only looks in the repository root directory, missing solutions in subdirectories.

**Solution:** Search recursively for `.sln` files, then `.csproj` if no solution found.

**File:** `extension/src/providers/workflowPanelProvider.ts`

```typescript
async function findSolutionPath(workspacePath: string): Promise<string | null> {
    // Recursively search for .sln files
    const slnFiles = await glob('**/*.sln', {
        cwd: workspacePath,
        ignore: ['**/node_modules/**', '**/bin/**', '**/obj/**'],
        maxDepth: 3
    });
    
    if (slnFiles.length > 0) {
        // Prefer shallowest path
        slnFiles.sort((a, b) => a.split('/').length - b.split('/').length);
        return path.join(workspacePath, slnFiles[0]);
    }
    
    // Fall back to .csproj
    const csprojFiles = await glob('**/*.csproj', {
        cwd: workspacePath,
        ignore: ['**/node_modules/**', '**/bin/**', '**/obj/**'],
        maxDepth: 3
    });
    
    return csprojFiles.length > 0 
        ? path.join(workspacePath, csprojFiles[0]) 
        : null;
}
```

**Acceptance Criteria:**
- [ ] `.sln` files found in subdirectories (up to depth 3)
- [ ] Shallowest `.sln` preferred when multiple found
- [ ] Falls back to `.csproj` if no `.sln`
- [ ] Ignores `node_modules`, `bin`, `obj` directories

---

### 2. Code Graph Status in Sidebar

**Status:** ✅ Completed 2025-12-12

See [code-graph-status-panel.md](../completed/code-graph-status-panel.md).

---

### 3. Indexing Progress in Status Bar

**Status:** 🔲 Not Started  
**Effort:** 2 hours

**Problem:** Users don't see indexing progress unless they open the output channel.

**Solution:** Add status bar item showing indexing progress with spinner.

**New File:** `extension/src/services/indexingStatusService.ts`

```typescript
export class IndexingStatusService {
    private statusBarItem: vscode.StatusBarItem;
    private pollInterval: NodeJS.Timeout | null = null;

    constructor(private apiService: AuraApiService) {
        this.statusBarItem = vscode.window.createStatusBarItem(
            vscode.StatusBarAlignment.Left, 100
        );
    }

    async startPolling(): Promise<void> {
        this.pollInterval = setInterval(async () => {
            const status = await this.apiService.getIndexingStatus();
            if (status.isProcessing) {
                this.statusBarItem.text = `$(sync~spin) Indexing: ${status.processedItems}/${status.totalItems}`;
                this.statusBarItem.show();
            } else if (status.processedItems > 0) {
                this.statusBarItem.text = `$(check) Indexed: ${status.processedItems} files`;
                this.statusBarItem.show();
                setTimeout(() => this.statusBarItem.hide(), 5000);
            } else {
                this.statusBarItem.hide();
            }
        }, 2000);
    }
}
```

**API Method:** `auraApiService.ts`

```typescript
interface IndexingStatus {
    queuedItems: number;
    processedItems: number;
    totalItems: number;
    failedItems: number;
    isProcessing: boolean;
    activeJobs: number;
}

async getIndexingStatus(): Promise<IndexingStatus> {
    const response = await this.fetch('/api/index/status');
    return response.json();
}
```

**Acceptance Criteria:**
- [ ] Status bar shows spinning icon during indexing
- [ ] Progress shows `X/Y files` format
- [ ] Completion notification shown for 5 seconds
- [ ] No polling when indexing is idle (optimization)

---

### 4. Prompt for Missing Code Graph

**Status:** 🔲 Not Started  
**Effort:** 1 hour

**Problem:** If RAG is indexed but Code Graph is not, users don't get prompted to index the Code Graph. They proceed without structural queries.

**Solution:** Update `handleEnrich` to check Code Graph status and prompt if missing.

**File:** `extension/src/providers/workflowPanelProvider.ts`

```typescript
async function handleEnrich(workflow: Workflow) {
    const ragStatus = await getDirectoryIndexStatus(workspacePath);
    
    if (!ragStatus.indexed) {
        // Existing: prompt to index codebase (does RAG + Code Graph)
        const shouldIndex = await vscode.window.showInformationMessage(
            'Codebase not indexed. Index now?',
            'Yes', 'No'
        );
        if (shouldIndex === 'Yes') {
            await handleIndexCodebase();
        }
        return;
    }
    
    // NEW: Check Code Graph separately
    const graphStatus = await apiService.getCodeGraphStatus(workspacePath);
    if (graphStatus.nodes === 0) {
        const indexGraph = await vscode.window.showInformationMessage(
            'Code Graph not indexed. Index for better structural queries?',
            'Yes', 'Skip'
        );
        if (indexGraph === 'Yes') {
            await handleIndexCodeGraph(); // New function to index Code Graph only
        }
    }
    
    // Continue with enrichment...
}
```

**New API Method:**

```typescript
interface CodeGraphStatus {
    nodes: number;
    edges: number;
    lastIndexed?: string;
}

async getCodeGraphStatus(workspacePath: string): Promise<CodeGraphStatus> {
    const response = await this.fetch(`/api/developer/graph/status?path=${encodeURIComponent(workspacePath)}`);
    return response.json();
}
```

**Acceptance Criteria:**
- [ ] Prompt shown when RAG indexed but Code Graph is not
- [ ] User can choose to index or skip
- [ ] "Skip" proceeds with enrichment (code graph optional)

---

### 5. Code Graph Query Commands

**Status:** 🔲 Not Started  
**Effort:** 2 hours  
**Priority:** Low

**Problem:** Code graph queries are only available via API. Users can't easily explore relationships.

**Solution:** Add VS Code commands for common queries.

**Commands to add:**

| Command | Description |
|---------|-------------|
| `aura.findImplementations` | Find classes implementing an interface |
| `aura.findCallers` | Find methods that call a given method |
| `aura.findDerivedTypes` | Find classes deriving from a base class |
| `aura.showTypeMembers` | Show all members of a type |

**File:** `extension/src/extension.ts`

```typescript
vscode.commands.registerCommand('aura.findImplementations', async () => {
    const typeName = await vscode.window.showInputBox({
        prompt: 'Enter interface or type name',
        placeHolder: 'IWorkflowService'
    });
    
    if (!typeName) return;
    
    const implementations = await apiService.findImplementations(typeName);
    
    // Show in QuickPick
    const selected = await vscode.window.showQuickPick(
        implementations.map(impl => ({
            label: impl.name,
            description: impl.filePath,
            detail: `Line ${impl.startLine}`,
            impl
        })),
        { placeHolder: `Implementations of ${typeName}` }
    );
    
    if (selected) {
        const doc = await vscode.workspace.openTextDocument(selected.impl.filePath);
        await vscode.window.showTextDocument(doc, {
            selection: new vscode.Range(selected.impl.startLine - 1, 0, selected.impl.startLine - 1, 0)
        });
    }
});
```

**Acceptance Criteria:**
- [ ] Commands accessible via Command Palette
- [ ] Results shown in QuickPick
- [ ] Clicking result opens file at correct line
- [ ] Error message if no results found

---

## Tray Application Progress (Deferred)

**Status:** Deferred to indexing-epic  

The tray application progress display has been moved to the [Indexing Epic](../unplanned/indexing-epic.md) as it requires tray app infrastructure work.

---

## Files to Modify

| File | Change |
|------|--------|
| `extension/src/providers/workflowPanelProvider.ts` | Recursive solution discovery, Code Graph prompt |
| `extension/src/services/auraApiService.ts` | `getIndexingStatus()`, `getCodeGraphStatus()`, query methods |
| `extension/src/services/indexingStatusService.ts` | New file for status bar polling |
| `extension/src/extension.ts` | Register status service, register query commands |
| `extension/package.json` | Add command contributions |

## Success Criteria

- [x] Status sidebar shows Code Graph health and stats (#2)
- [ ] Recursive solution discovery finds `.sln` in subdirectories (#1)
- [ ] Status bar shows indexing progress with spinner (#3)
- [ ] User prompted if Code Graph missing when RAG present (#4)
- [ ] Code Graph query commands available in palette (#5)
