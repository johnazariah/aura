<#
.SYNOPSIS
    Aura Story module for orchestrator parallel dispatch.

.DESCRIPTION
    PowerShell module with cmdlets for managing Aura stories with parallel task dispatch.
    This module provides:
    - New-AuraStory: Create a new story with optional worktree
    - Get-AuraStory: Get story details
    - Get-AuraStories: List all stories
    - Start-AuraStoryDecompose: Decompose story into parallelizable tasks

.NOTES
    Part of the Aura Orchestrator Parallel Dispatch feature.
#>

$script:DefaultBaseUrl = "http://localhost:5300"

function Get-AuraApiUrl {
    param([string]$BaseUrl)
    if ($BaseUrl) { return $BaseUrl }
    if ($env:AURA_API_URL) { return $env:AURA_API_URL }
    return $script:DefaultBaseUrl
}

function Invoke-AuraApi {
    param(
        [Parameter(Mandatory)]
        [string]$Endpoint,

        [ValidateSet("GET", "POST", "PUT", "DELETE")]
        [string]$Method = "GET",

        [object]$Body,

        [string]$BaseUrl
    )

    $url = "$(Get-AuraApiUrl $BaseUrl)$Endpoint"

    $params = @{
        Uri = $url
        Method = $Method
        ContentType = "application/json"
    }

    if ($Body) {
        $params.Body = $Body | ConvertTo-Json -Depth 10
    }

    try {
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        $errorMessage = $_.Exception.Message
        if ($_.ErrorDetails.Message) {
            try {
                $errorBody = $_.ErrorDetails.Message | ConvertFrom-Json
                if ($errorBody.error) {
                    $errorMessage = $errorBody.error
                }
            }
            catch {
                $errorMessage = $_.ErrorDetails.Message
            }
        }
        throw "Aura API error: $errorMessage"
    }
}

<#
.SYNOPSIS
    Creates a new Aura story.

.DESCRIPTION
    Creates a new story with optional git worktree and returns the story object.

.PARAMETER Title
    The story title (required).

.PARAMETER Description
    The story description (optional).

.PARAMETER RepositoryPath
    The repository path for worktree creation (optional).

.PARAMETER AutomationMode
    The automation mode: Assisted, Autonomous, or FullAutonomous. Defaults to Assisted.

.PARAMETER IssueUrl
    Optional GitHub issue URL to link to this story.

.PARAMETER BaseUrl
    The Aura API base URL. Defaults to http://localhost:5300 or $env:AURA_API_URL.

.EXAMPLE
    New-AuraStory -Title "Add user validation" -RepositoryPath "C:\repos\myapp"

.EXAMPLE
    New-AuraStory -Title "Fix bug #123" -IssueUrl "https://github.com/org/repo/issues/123"

.OUTPUTS
    PSCustomObject with story properties: id, title, status, worktreePath, gitBranch, etc.
#>
function New-AuraStory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Title,

        [Parameter(Position = 1)]
        [string]$Description,

        [string]$RepositoryPath,

        [ValidateSet("Assisted", "Autonomous", "FullAutonomous")]
        [string]$AutomationMode = "Assisted",

        [string]$IssueUrl,

        [string]$BaseUrl
    )

    $body = @{
        title = $Title
        automationMode = $AutomationMode
    }

    if ($Description) { $body.description = $Description }
    if ($RepositoryPath) { $body.repositoryPath = $RepositoryPath }
    if ($IssueUrl) { $body.issueUrl = $IssueUrl }

    $result = Invoke-AuraApi -Endpoint "/api/developer/stories" -Method POST -Body $body -BaseUrl $BaseUrl

    Write-Host "✅ Created story: $($result.title)" -ForegroundColor Green
    if ($result.worktreePath) {
        Write-Host "   Worktree: $($result.worktreePath)" -ForegroundColor Cyan
        Write-Host "   Branch:   $($result.gitBranch)" -ForegroundColor Cyan
    }

    return $result
}

<#
.SYNOPSIS
    Gets an Aura story by ID.

.DESCRIPTION
    Retrieves a story with all its details including steps.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Get-AuraStory -Id "12345678-1234-1234-1234-123456789abc"

.EXAMPLE
    $story = Get-AuraStory $storyId
#>
function Get-AuraStory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$BaseUrl
    )

    return Invoke-AuraApi -Endpoint "/api/developer/stories/$Id" -BaseUrl $BaseUrl
}

<#
.SYNOPSIS
    Lists all Aura stories.

.DESCRIPTION
    Retrieves all stories, optionally filtered by status or repository path.

.PARAMETER Status
    Filter by status: Created, Analyzing, Analyzed, Planning, Planned, Executing, Completed, Failed, Cancelled.

.PARAMETER RepositoryPath
    Filter by repository path.

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Get-AuraStories

.EXAMPLE
    Get-AuraStories -Status Planned

.EXAMPLE
    Get-AuraStories -RepositoryPath "C:\repos\myapp"
#>
function Get-AuraStories {
    [CmdletBinding()]
    param(
        [ValidateSet("Created", "Analyzing", "Analyzed", "Planning", "Planned", "Executing", "Completed", "Failed", "Cancelled")]
        [string]$Status,

        [string]$RepositoryPath,

        [string]$BaseUrl
    )

    $query = @()
    if ($Status) { $query += "status=$Status" }
    if ($RepositoryPath) { $query += "repositoryPath=$([Uri]::EscapeDataString($RepositoryPath))" }

    $endpoint = "/api/developer/stories"
    if ($query.Count -gt 0) {
        $endpoint += "?" + ($query -join "&")
    }

    $result = Invoke-AuraApi -Endpoint $endpoint -BaseUrl $BaseUrl
    return $result.stories
}

<#
.SYNOPSIS
    Analyzes a story to gather context.

.DESCRIPTION
    Runs the analysis phase on a story to gather codebase context.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Start-AuraStoryAnalysis -Id $storyId
#>
function Start-AuraStoryAnalysis {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$BaseUrl
    )

    $result = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/analyze" -Method POST -BaseUrl $BaseUrl

    Write-Host "✅ Analysis complete for story: $($result.id)" -ForegroundColor Green
    Write-Host "   Status: $($result.status)" -ForegroundColor Cyan

    return $result
}

<#
.SYNOPSIS
    Decomposes a story into parallelizable tasks.

.DESCRIPTION
    Uses an LLM to break down the story into tasks organized into waves.
    Tasks in the same wave can be executed in parallel by multiple agents.

.PARAMETER Id
    The story ID (GUID). Story must be in Analyzed or Planned status.

.PARAMETER MaxParallelism
    Maximum number of parallel agents per wave. Defaults to 4.

.PARAMETER IncludeTests
    Whether to include test generation tasks. Defaults to $true.

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Start-AuraStoryDecompose -Id $storyId

.EXAMPLE
    Start-AuraStoryDecompose -Id $storyId -MaxParallelism 8 -IncludeTests $false

.OUTPUTS
    PSCustomObject with storyId, tasks array, and waveCount.
#>
function Start-AuraStoryDecompose {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [int]$MaxParallelism = 4,

        [bool]$IncludeTests = $true,

        [string]$BaseUrl
    )

    $body = @{
        maxParallelism = $MaxParallelism
        includeTests = $IncludeTests
    }

    $result = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/decompose" -Method POST -Body $body -BaseUrl $BaseUrl

    Write-Host "✅ Decomposed story into $($result.tasks.Count) tasks across $($result.waveCount) waves" -ForegroundColor Green
    Write-Host ""

    # Group tasks by wave for display
    $tasksByWave = $result.tasks | Group-Object -Property wave | Sort-Object Name

    foreach ($waveGroup in $tasksByWave) {
        Write-Host "Wave $($waveGroup.Name):" -ForegroundColor Yellow
        foreach ($task in $waveGroup.Group) {
            $deps = if ($task.dependsOn -and $task.dependsOn.Count -gt 0) {
                " (depends on: $($task.dependsOn -join ', '))"
            } else { "" }
            Write-Host "  [$($task.id)] $($task.title)$deps" -ForegroundColor White
        }
        Write-Host ""
    }

    return $result
}

<#
.SYNOPSIS
    Runs the orchestrator to execute decomposed tasks.

.DESCRIPTION
    Dispatches tasks to GitHub Copilot CLI agents in parallel.
    Executes one wave at a time with quality gates between waves.
    Call this repeatedly to progress through all waves.

.PARAMETER Id
    The story ID (GUID). Story must be decomposed.

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Start-AuraStory -Id $storyId

.EXAMPLE
    # Run all waves until completion
    do {
        $result = Start-AuraStory -Id $storyId
    } while (-not $result.isComplete -and -not $result.error)

.OUTPUTS
    PSCustomObject with status, currentWave, completedTasks, failedTasks, etc.
#>
function Start-AuraStory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$BaseUrl
    )

    $result = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/run" -Method POST -BaseUrl $BaseUrl

    if ($result.isComplete) {
        Write-Host "✅ Story orchestration complete!" -ForegroundColor Green
        Write-Host "   Completed tasks: $($result.completedTasks.Count)" -ForegroundColor Cyan
    }
    elseif ($result.waitingForGate) {
        Write-Host "⏸️  Wave $($result.currentWave) complete, waiting for quality gate" -ForegroundColor Yellow
        Write-Host "   Run Start-AuraStory again to continue" -ForegroundColor Gray
    }
    elseif ($result.error) {
        Write-Host "❌ Orchestration failed: $($result.error)" -ForegroundColor Red
        if ($result.failedTasks.Count -gt 0) {
            Write-Host "   Failed tasks:" -ForegroundColor Red
            foreach ($task in $result.failedTasks) {
                Write-Host "     - $($task.title): $($task.error)" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "🔄 Running wave $($result.currentWave)/$($result.totalWaves)..." -ForegroundColor Cyan
        Write-Host "   Started: $($result.startedTasks.Count) tasks" -ForegroundColor Gray
    }

    return $result
}

<#
.SYNOPSIS
    Gets the current orchestrator status for a story.

.DESCRIPTION
    Returns the current state of all tasks, which wave is running, etc.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Get-AuraStoryStatus -Id $storyId
#>
function Get-AuraStoryStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$BaseUrl
    )

    $result = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/orchestrator-status" -BaseUrl $BaseUrl

    Write-Host "Story Status: $($result.status)" -ForegroundColor Cyan
    Write-Host "Wave: $($result.currentWave)/$($result.totalWaves)" -ForegroundColor Gray
    Write-Host "Max Parallelism: $($result.maxParallelism)" -ForegroundColor Gray
    Write-Host ""

    if ($result.tasks.Count -gt 0) {
        $tasksByWave = $result.tasks | Group-Object -Property wave | Sort-Object Name

        foreach ($waveGroup in $tasksByWave) {
            Write-Host "Wave $($waveGroup.Name):" -ForegroundColor Yellow
            foreach ($task in $waveGroup.Group) {
                $statusIcon = switch ($task.status) {
                    "Pending" { "⏳" }
                    "Running" { "🔄" }
                    "Completed" { "✅" }
                    "Failed" { "❌" }
                    "Skipped" { "⏭️" }
                    default { "❓" }
                }
                Write-Host "  $statusIcon [$($task.id)] $($task.title) - $($task.status)" -ForegroundColor White
            }
            Write-Host ""
        }
    }

    return $result
}

<#
.SYNOPSIS
    Runs the full story workflow: analyze, decompose, and run all waves.

.DESCRIPTION
    Convenience function that runs the complete orchestration workflow.
    Handles analysis, decomposition, and all execution waves automatically.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER MaxParallelism
    Maximum number of parallel agents per wave. Defaults to 4.

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Invoke-AuraStoryFull -Id $storyId
#>
function Invoke-AuraStoryFull {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [int]$MaxParallelism = 4,

        [string]$BaseUrl
    )

    Write-Host "🚀 Starting full story orchestration..." -ForegroundColor Cyan
    Write-Host ""

    # Step 1: Get current status
    $story = Get-AuraStory -Id $Id -BaseUrl $BaseUrl

    # Step 2: Analyze if needed
    if ($story.status -eq "Created") {
        Write-Host "📊 Analyzing story..." -ForegroundColor Yellow
        $story = Start-AuraStoryAnalysis -Id $Id -BaseUrl $BaseUrl
    }

    # Step 3: Decompose if needed
    $status = Get-AuraStoryStatus -Id $Id -BaseUrl $BaseUrl
    if ($status.status -eq "NotDecomposed") {
        Write-Host "🔀 Decomposing into tasks..." -ForegroundColor Yellow
        $decomposed = Start-AuraStoryDecompose -Id $Id -MaxParallelism $MaxParallelism -BaseUrl $BaseUrl
    }

    # Step 4: Run all waves
    Write-Host "⚡ Running all waves..." -ForegroundColor Yellow
    $attempt = 0
    $maxAttempts = 20  # Safety limit

    do {
        $attempt++
        $result = Start-AuraStory -Id $Id -BaseUrl $BaseUrl

        if ($result.error) {
            Write-Host "❌ Failed at wave $($result.currentWave): $($result.error)" -ForegroundColor Red
            return $result
        }

        if (-not $result.isComplete -and $result.waitingForGate) {
            Write-Host "   Continuing to next wave..." -ForegroundColor Gray
            Start-Sleep -Milliseconds 500
        }
    } while (-not $result.isComplete -and $attempt -lt $maxAttempts)

    if ($result.isComplete) {
        Write-Host ""
        Write-Host "🎉 Story orchestration complete!" -ForegroundColor Green
        Write-Host "   Total tasks completed: $($result.completedTasks.Count)" -ForegroundColor Cyan
    }

    return $result
}

<#
.SYNOPSIS
    Completes a story and triggers the merge ceremony.

.DESCRIPTION
    Marks the story as complete, runs verification, commits changes,
    and optionally creates a pull request.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER CommitMessage
    Optional custom commit message.

.PARAMETER CreatePullRequest
    Whether to create a pull request. Defaults to $true.

.PARAMETER PrTitle
    Optional PR title. Defaults to story title.

.PARAMETER Draft
    Whether to create the PR as a draft. Defaults to $true.

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Complete-AuraStory -Id $storyId

.EXAMPLE
    Complete-AuraStory -Id $storyId -CreatePullRequest $false
#>
function Complete-AuraStory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$CommitMessage,

        [bool]$CreatePullRequest = $true,

        [string]$PrTitle,

        [bool]$Draft = $true,

        [string]$BaseUrl
    )

    Write-Host "🏁 Completing story..." -ForegroundColor Cyan

    # First complete the story (runs verification)
    $completeResult = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/complete" -Method POST -BaseUrl $BaseUrl

    if ($completeResult.verificationPassed -eq $false) {
        Write-Host "⚠️  Verification failed!" -ForegroundColor Yellow
        Write-Host "   Story completed but verification issues found." -ForegroundColor Yellow
    }
    else {
        Write-Host "✅ Verification passed" -ForegroundColor Green
    }

    # Then finalize (commit, squash, PR)
    $body = @{
        createPullRequest = $CreatePullRequest
        draft = $Draft
    }

    if ($CommitMessage) { $body.commitMessage = $CommitMessage }
    if ($PrTitle) { $body.prTitle = $PrTitle }

    $finalizeResult = Invoke-AuraApi -Endpoint "/api/developer/stories/$Id/finalize" -Method POST -Body $body -BaseUrl $BaseUrl

    if ($finalizeResult.pullRequestUrl) {
        Write-Host "🔗 Pull request created: $($finalizeResult.pullRequestUrl)" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Host "🎉 Story complete!" -ForegroundColor Green

    return @{
        story = $completeResult
        finalize = $finalizeResult
    }
}

<#
.SYNOPSIS
    Removes the worktree for a completed story.

.DESCRIPTION
    Cleans up the git worktree after a story is merged.
    Should be called after the PR is merged.

.PARAMETER Id
    The story ID (GUID).

.PARAMETER BaseUrl
    The Aura API base URL.

.EXAMPLE
    Remove-AuraStoryWorktree -Id $storyId
#>
function Remove-AuraStoryWorktree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [Guid]$Id,

        [string]$BaseUrl
    )

    $story = Get-AuraStory -Id $Id -BaseUrl $BaseUrl

    if (-not $story.worktreePath) {
        Write-Host "Story has no worktree to remove" -ForegroundColor Yellow
        return
    }

    if (Test-Path $story.worktreePath) {
        Write-Host "🗑️  Removing worktree: $($story.worktreePath)" -ForegroundColor Yellow

        # Use git to remove the worktree
        Push-Location $story.repositoryPath
        try {
            git worktree remove $story.worktreePath --force
            Write-Host "✅ Worktree removed" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host "Worktree path does not exist: $($story.worktreePath)" -ForegroundColor Gray
    }
}

# Export module members
Export-ModuleMember -Function @(
    'New-AuraStory'
    'Get-AuraStory'
    'Get-AuraStories'
    'Start-AuraStoryAnalysis'
    'Start-AuraStoryDecompose'
    'Start-AuraStory'
    'Get-AuraStoryStatus'
    'Invoke-AuraStoryFull'
    'Complete-AuraStory'
    'Remove-AuraStoryWorktree'
)
