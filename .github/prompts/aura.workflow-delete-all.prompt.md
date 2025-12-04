# Delete All Workflows

Delete all workflows from the Aura system.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Workflows.ps1 -Action DeleteAll
```

This will prompt for confirmation before deleting.

## Note

There is no single endpoint to delete all workflows. You must:
1. Get all workflows
2. Delete each one individually

## Commands

### PowerShell Script to Delete All
```powershell
# Get all workflows and delete each one
$workflows = Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows"
foreach ($workflow in $workflows) {
    Write-Host "Deleting workflow: $($workflow.id) - $($workflow.title)"
    Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows/$($workflow.id)" -Method Delete
}
Write-Host "All workflows deleted."
```

### One-liner
```powershell
(Invoke-RestMethod http://localhost:5300/api/developer/workflows) | ForEach-Object { Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows/$($_.id)" -Method Delete; Write-Host "Deleted: $($_.title)" }
```

## Verification

Confirm all workflows are deleted:
```powershell
curl http://localhost:5300/api/developer/workflows
```

Should return: `[]`
