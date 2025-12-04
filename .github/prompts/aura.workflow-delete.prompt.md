# Delete Workflow by ID

Delete a specific workflow by its ID.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Workflows.ps1 -Action Delete -Id "YOUR-WORKFLOW-GUID"
```

## API Endpoint

```
DELETE http://localhost:5300/api/developer/workflows/{id}
```

## Command

```powershell
curl -X DELETE http://localhost:5300/api/developer/workflows/YOUR-WORKFLOW-GUID-HERE
```

Example:
```powershell
curl -X DELETE http://localhost:5300/api/developer/workflows/12345678-1234-1234-1234-123456789abc
```

Or with Invoke-RestMethod:
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows/YOUR-GUID" -Method Delete
```

## Response

- `204 No Content`: Workflow successfully deleted
- `404 Not Found`: Workflow with the specified ID does not exist
