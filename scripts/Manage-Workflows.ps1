<#
.SYNOPSIS
    Manage Aura workflows via the API.

.DESCRIPTION
    Create, list, get, and delete workflows.

.PARAMETER Action
    The action to perform: List, Get, Create, Delete, DeleteAll

.PARAMETER Id
    Workflow ID (required for Get and Delete actions)

.PARAMETER Title
    Workflow title (required for Create action)

.PARAMETER Description
    Workflow description (optional for Create action)

.PARAMETER RepositoryPath
    Repository path (optional for Create and List actions)

.PARAMETER BaseUrl
    The API base URL. Defaults to http://localhost:5300

.EXAMPLE
    .\Manage-Workflows.ps1 -Action List

.EXAMPLE
    .\Manage-Workflows.ps1 -Action Create -Title "Fix bug #123" -Description "Fix the login issue" -RepositoryPath "C:\work\Brightsword"

.EXAMPLE
    .\Manage-Workflows.ps1 -Action Get -Id "12345678-1234-1234-1234-123456789abc"

.EXAMPLE
    .\Manage-Workflows.ps1 -Action Delete -Id "12345678-1234-1234-1234-123456789abc"

.EXAMPLE
    .\Manage-Workflows.ps1 -Action DeleteAll
#>

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("List", "Get", "Create", "Delete", "DeleteAll")]
    [string]$Action,
    
    [string]$Id,
    [string]$Title,
    [string]$Description,
    [string]$RepositoryPath,
    [string]$BaseUrl = "http://localhost:5300"
)

$ErrorActionPreference = "Stop"
$apiUrl = "$BaseUrl/api/developer/workflows"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Format-Workflow {
    param($Workflow)
    
    $status = switch ($Workflow.status) {
        "Pending" { "⏳ Pending" }
        "InProgress" { "🔄 In Progress" }
        "Completed" { "✅ Completed" }
        "Failed" { "❌ Failed" }
        default { $Workflow.status }
    }
    
    Write-Host "ID:          " -NoNewline -ForegroundColor Gray
    Write-Host $Workflow.id -ForegroundColor Yellow
    Write-Host "Title:       " -NoNewline -ForegroundColor Gray
    Write-Host $Workflow.title
    Write-Host "Status:      " -NoNewline -ForegroundColor Gray
    Write-Host $status
    if ($Workflow.description) {
        Write-Host "Description: " -NoNewline -ForegroundColor Gray
        Write-Host $Workflow.description
    }
    if ($Workflow.repositoryPath) {
        Write-Host "Repository:  " -NoNewline -ForegroundColor Gray
        Write-Host $Workflow.repositoryPath
    }
    Write-Host "Created:     " -NoNewline -ForegroundColor Gray
    Write-Host $Workflow.createdAt
    if ($Workflow.steps -and $Workflow.steps.Count -gt 0) {
        Write-Host "Steps:       " -NoNewline -ForegroundColor Gray
        Write-Host "$($Workflow.steps.Count) steps"
    }
    Write-Host ""
}

try {
    switch ($Action) {
        "List" {
            Write-Header "WORKFLOW LIST"
            
            $url = $apiUrl
            if ($RepositoryPath) {
                $encodedPath = [System.Web.HttpUtility]::UrlEncode($RepositoryPath)
                $url = "$apiUrl?repositoryPath=$encodedPath"
            }
            
            $workflows = Invoke-RestMethod -Uri $url -Method Get
            
            if ($workflows.Count -eq 0) {
                Write-Host "No workflows found." -ForegroundColor Yellow
            } else {
                Write-Host "Found $($workflows.Count) workflow(s):" -ForegroundColor Green
                Write-Host ""
                foreach ($wf in $workflows) {
                    Format-Workflow $wf
                    Write-Host "---" -ForegroundColor Gray
                }
            }
        }
        
        "Get" {
            if (-not $Id) {
                Write-Host "Error: -Id is required for Get action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "WORKFLOW DETAILS"
            
            $workflow = Invoke-RestMethod -Uri "$apiUrl/$Id" -Method Get
            Format-Workflow $workflow
        }
        
        "Create" {
            if (-not $Title) {
                Write-Host "Error: -Title is required for Create action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "CREATE WORKFLOW"
            
            $body = @{ title = $Title }
            if ($Description) { $body.description = $Description }
            if ($RepositoryPath) { $body.repositoryPath = $RepositoryPath }
            
            $json = $body | ConvertTo-Json
            $workflow = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $json -ContentType "application/json"
            
            Write-Host "✅ Workflow created successfully!" -ForegroundColor Green
            Write-Host ""
            Format-Workflow $workflow
        }
        
        "Delete" {
            if (-not $Id) {
                Write-Host "Error: -Id is required for Delete action" -ForegroundColor Red
                exit 1
            }
            
            Write-Header "DELETE WORKFLOW"
            
            Invoke-RestMethod -Uri "$apiUrl/$Id" -Method Delete | Out-Null
            Write-Host "✅ Workflow $Id deleted successfully!" -ForegroundColor Green
        }
        
        "DeleteAll" {
            Write-Header "DELETE ALL WORKFLOWS"
            
            $workflows = Invoke-RestMethod -Uri $apiUrl -Method Get
            
            if ($workflows.Count -eq 0) {
                Write-Host "No workflows to delete." -ForegroundColor Yellow
                exit 0
            }
            
            Write-Host "Found $($workflows.Count) workflow(s) to delete:" -ForegroundColor Yellow
            foreach ($wf in $workflows) {
                Write-Host "  - $($wf.title) ($($wf.id))" -ForegroundColor Gray
            }
            Write-Host ""
            
            $confirm = Read-Host "Are you sure you want to delete all workflows? (y/N)"
            if ($confirm -ne "y" -and $confirm -ne "Y") {
                Write-Host "Cancelled." -ForegroundColor Yellow
                exit 0
            }
            
            Write-Host ""
            foreach ($wf in $workflows) {
                Write-Host "Deleting: $($wf.title)..." -NoNewline
                Invoke-RestMethod -Uri "$apiUrl/$($wf.id)" -Method Delete | Out-Null
                Write-Host " ✅" -ForegroundColor Green
            }
            
            Write-Host ""
            Write-Host "✅ All $($workflows.Count) workflows deleted!" -ForegroundColor Green
        }
    }
}
catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}
