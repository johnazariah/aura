---
title: "ADR-007: VS Code Extension Testing Patterns"
status: "Accepted"
date: "2026-01-30"
authors: "Anvil Team"
tags: ["vscode", "extension", "testing", "automation"]
supersedes: ""
superseded_by: ""
---

# ADR-007: VS Code Extension Testing Patterns

## Status

Accepted

## Context

The Aura VS Code Extension is a critical component of the system. Anvil must test that:
- The extension loads correctly
- Commands execute properly
- The UI (webviews, tree views) renders and responds
- Extension integrates correctly with the Aura backend

Testing VS Code extensions from an external harness is complex because:
- VS Code runs in Electron (separate process)
- Extensions have access to VS Code APIs that require the host
- UI automation requires interaction with the Extension Host

## Decision

We adopt **VS Code Extension Testing** using `@vscode/test-electron` to launch VS Code with the Aura extension and automate it programmatically.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Anvil CLI                                │
│                     (Test Orchestrator)                          │
└─────────────────────────┬───────────────────────────────────────┘
                          │ Spawns
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    VS Code Instance                              │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                  Aura Extension                              ││
│  │  - Commands registered                                       ││
│  │  - Webviews rendered                                         ││
│  │  - Tree views populated                                      ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │                  Test Runner Extension                       ││
│  │  - Receives commands from Anvil                              ││
│  │  - Executes VS Code API calls                                ││
│  │  - Reports results back                                      ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
```

### Testing Approaches

| Approach | Use Case | Implementation |
|----------|----------|----------------|
| **Command Execution** | Test extension commands work | `vscode.commands.executeCommand()` |
| **API Verification** | Test extension APIs respond | Direct extension API calls |
| **UI Automation** | Test webviews, tree views | Synthetic events + DOM queries |
| **Integration** | End-to-end with Aura backend | Full workflow execution |

### Test Runner Extension

Anvil includes a minimal "test runner" extension that:
1. Loads alongside the Aura extension
2. Exposes an IPC channel for receiving test commands
3. Executes VS Code API calls on behalf of Anvil
4. Reports results back via IPC

```typescript
// anvil-test-runner/src/extension.ts
import * as vscode from 'vscode';
import { createServer } from 'net';

export function activate(context: vscode.ExtensionContext) {
    const server = createServer((socket) => {
        socket.on('data', async (data) => {
            const command = JSON.parse(data.toString());
            const result = await executeTestCommand(command);
            socket.write(JSON.stringify(result));
        });
    });
    server.listen(9876); // Anvil connects here
}

async function executeTestCommand(cmd: TestCommand): Promise<TestResult> {
    switch (cmd.type) {
        case 'executeCommand':
            return await vscode.commands.executeCommand(cmd.command, ...cmd.args);
        case 'getTreeItems':
            // Query tree view contents
        case 'checkWebview':
            // Verify webview state
    }
}
```

### Anvil Test Client (C#)

```csharp
public class VsCodeTestClient : IDisposable
{
    private readonly Process _vscodeProcess;
    private readonly TcpClient _ipcClient;

    public static async Task<VsCodeTestClient> LaunchAsync(
        string workspacePath,
        string extensionPath,
        CancellationToken ct)
    {
        // Launch VS Code with extensions
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "code",
            Arguments = $"--extensionDevelopmentPath={extensionPath} " +
                       $"--extensionTestsPath={testRunnerPath} " +
                       $"\"{workspacePath}\"",
            UseShellExecute = false
        });

        // Connect to test runner IPC
        var client = new TcpClient();
        await client.ConnectAsync("localhost", 9876, ct);

        return new VsCodeTestClient(process, client);
    }

    public async Task<Result<T, TestError>> ExecuteCommandAsync<T>(
        string command, 
        params object[] args)
    {
        var request = new { type = "executeCommand", command, args };
        await SendAsync(request);
        return await ReceiveAsync<T>();
    }

    public async Task<Result<TreeItem[], TestError>> GetTreeItemsAsync(string viewId)
    {
        var request = new { type = "getTreeItems", viewId };
        await SendAsync(request);
        return await ReceiveAsync<TreeItem[]>();
    }
}
```

### Example Test

```csharp
[Fact]
public async Task Should_ShowStories_When_ExtensionActivates()
{
    // Test Doc:
    // - Why: Verify the Stories tree view populates on activation
    // - Contract: Tree view shows stories from the workspace
    // - Usage Notes: Requires Aura service running
    // - Quality Contribution: Catches extension activation failures
    // - Worked Example: Open workspace → tree shows "story-1", "story-2"

    // Arrange
    await using var vscode = await VsCodeTestClient.LaunchAsync(
        workspacePath: "fixtures/sample-workspace",
        extensionPath: "path/to/aura-extension",
        ct);

    // Act
    var treeItems = await vscode.GetTreeItemsAsync("aura.storiesView");

    // Assert
    treeItems.Should().BeSuccess();
    treeItems.Value.Should().Contain(item => item.Label == "story-1");
}
```

### Test Fixtures

```
anvil/
├── fixtures/
│   ├── sample-workspace/      # Workspace for tests
│   │   ├── .vscode/
│   │   │   └── settings.json
│   │   └── stories/
│   │       └── hello-world.yaml
│   └── extensions/
│       └── anvil-test-runner/ # Test runner extension
```

### Execution Modes

| Mode | Description | Speed |
|------|-------------|-------|
| **Headless** | VS Code runs without UI (CI) | Fast |
| **Headed** | VS Code visible (debugging) | Slower |
| **Existing Instance** | Attach to running VS Code | Fastest |

```csharp
var options = new VsCodeLaunchOptions
{
    Headless = Environment.GetEnvironmentVariable("CI") == "true",
    Timeout = TimeSpan.FromSeconds(30),
    ExtensionDevelopmentPath = auraExtensionPath
};
```

## Consequences

**Positive**
- **POS-001**: Tests the actual extension in the real VS Code environment
- **POS-002**: Can verify UI elements (tree views, webviews, commands)
- **POS-003**: Catches integration issues between extension and VS Code APIs
- **POS-004**: Supports both headless (CI) and headed (debug) modes
- **POS-005**: Most important testing facet covered properly

**Negative**
- **NEG-001**: Complex setup (requires VS Code, extension build)
- **NEG-002**: Slower than unit tests (launches full VS Code)
- **NEG-003**: Test runner extension adds maintenance burden
- **NEG-004**: IPC communication adds complexity
- **NEG-005**: May be flaky due to VS Code startup timing

## Alternatives Considered

### Alternative 1: REST API Only
- **Description**: Only test via Aura REST API, not the extension
- **Rejection Reason**: Misses critical extension functionality; UI bugs would go undetected

### Alternative 2: Extension Unit Tests Only
- **Description**: Test extension with mocked VS Code APIs
- **Rejection Reason**: Doesn't test real integration; mocks may not match actual behavior

### Alternative 3: Manual Testing
- **Description**: Human testers verify extension functionality
- **Rejection Reason**: Not automated; doesn't scale; misses regressions

## Implementation Notes

- **IMP-001**: Use `@vscode/test-electron` npm package for launching VS Code
- **IMP-002**: Test runner extension communicates via TCP socket (simple, cross-platform)
- **IMP-003**: Set reasonable timeouts (VS Code startup can be slow)
- **IMP-004**: Run in headless mode in CI (`--disable-gpu --headless`)
- **IMP-005**: Clean up VS Code processes after tests
- **IMP-006**: Consider using VS Code's built-in test runner for simpler cases

## References

- [VS Code Extension Testing](https://code.visualstudio.com/api/working-with-extensions/testing-extension)
- [@vscode/test-electron](https://github.com/microsoft/vscode-test)
- [Extension Test Runner](https://code.visualstudio.com/api/working-with-extensions/testing-extension#the-test-runner-script)
- [Aura Extension](../../extension/README.md)
