# Get Workflow by ID

Retrieve a specific workflow by its ID.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Workflows.ps1 -Action Get -Id "YOUR-WORKFLOW-GUID"
```

## API Endpoint

```
GET http://localhost:5300/api/developer/workflows/{id}
```

## Command

```powershell
curl http://localhost:5300/api/developer/workflows/YOUR-WORKFLOW-GUID-HERE
```

Example:
```powershell
curl http://localhost:5300/api/developer/workflows/12345678-1234-1234-1234-123456789abc
```

## Response

Returns the workflow object:
```json
{
  "id": "guid",
  "title": "Workflow Title",
  "description": "Description",
  "repositoryPath": "C:\\work\\Repo",
  "status": "Pending",
  "steps": [],
  "createdAt": "2025-12-04T...",
  "updatedAt": "2025-12-04T..."
}
```

## Error Responses

- `404 Not Found`: Workflow with the specified ID does not exist
