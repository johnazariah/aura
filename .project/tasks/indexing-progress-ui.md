# Task: Indexing Progress in Tray and VS Code Status Bar

## Overview
Display background indexing progress in both the system tray application and VS Code extension status bar.

## Requirements

### API Endpoints (Already Exist)
- `GET /api/index/status` - Overall indexer status
- `GET /api/index/jobs/{jobId}` - Specific job progress

### Tray Application Updates

**File:** `src/Aura.Tray/`

1. **Add polling for indexing status** in the tray menu/tooltip
2. **Show progress** when indexing is active:
   - Tooltip: "Aura - Indexing: 45% (123/456 files)"
   - Menu item: "Indexing Progress: 45%"
3. **Notification** when indexing completes or fails
4. **Icon change** during indexing (optional - animated or different icon)

**Implementation:**
```csharp
// Add to TrayService or create IndexingMonitor
public class IndexingMonitor : BackgroundService
{
    private readonly HttpClient _httpClient;
    private readonly ITrayIcon _trayIcon;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var status = await GetIndexingStatus();
            if (status.IsProcessing)
            {
                _trayIcon.SetTooltip($"Aura - Indexing: {status.ActiveJobs} jobs");
            }
            else
            {
                _trayIcon.SetTooltip("Aura - Ready");
            }
            await Task.Delay(2000, stoppingToken);
        }
    }
}
```

### VS Code Extension Updates

**File:** `extension/src/providers/statusTreeProvider.ts`

1. **Add status bar item** for indexing progress
2. **Poll the API** every 2 seconds when indexing
3. **Show spinner** during indexing
4. **Show completion notification**

**Implementation:**
```typescript
// extension/src/services/indexingStatusService.ts
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
                this.statusBarItem.text = `$(sync~spin) Indexing: ${status.activeJobs} jobs`;
                this.statusBarItem.show();
            } else if (status.processedItems > 0) {
                this.statusBarItem.text = `$(check) Indexed: ${status.processedItems} files`;
                setTimeout(() => this.statusBarItem.hide(), 5000);
            } else {
                this.statusBarItem.hide();
            }
        }, 2000);
    }

    stopPolling(): void {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
            this.pollInterval = null;
        }
    }
}
```

**Add to extension.ts:**
```typescript
// In activate()
const indexingService = new IndexingStatusService(apiService);
indexingService.startPolling();

// In deactivate()
indexingService.stopPolling();
```

**Add API method in auraApiService.ts:**
```typescript
async getIndexingStatus(): Promise<IndexingStatus> {
    const response = await this.fetch('/api/index/status');
    return response.json();
}

async getJobStatus(jobId: string): Promise<JobStatus> {
    const response = await this.fetch(`/api/index/jobs/${jobId}`);
    return response.json();
}

interface IndexingStatus {
    queuedItems: number;
    processedItems: number;
    failedItems: number;
    isProcessing: boolean;
    activeJobs: number;
}

interface JobStatus {
    jobId: string;
    source: string;
    state: string;
    totalItems: number;
    processedItems: number;
    failedItems: number;
    progressPercent: number;
    startedAt?: string;
    completedAt?: string;
    error?: string;
}
```

## Acceptance Criteria

1. [ ] Tray tooltip shows indexing progress when active
2. [ ] Tray shows notification when indexing completes
3. [ ] VS Code status bar shows spinning icon during indexing
4. [ ] VS Code status bar shows progress percentage
5. [ ] VS Code shows info message when indexing completes
6. [ ] Both UIs update every 2 seconds during indexing
7. [ ] No polling when indexing is idle (optional optimization)

## Priority
Medium - Nice to have for UX, but indexing works without it

## Estimated Effort
- Tray: 2-3 hours
- VS Code Extension: 1-2 hours
