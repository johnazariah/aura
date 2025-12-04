# Get All Workflows

Retrieve all workflows from the Aura system.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Workflows.ps1 -Action List

# Filter by repository
c:\work\aura\scripts\Manage-Workflows.ps1 -Action List -RepositoryPath "C:\work\Brightsword"
```

## API Endpoint

```
GET http://localhost:5300/api/developer/workflows
```

## Optional Query Parameters

- `repositoryPath`: Filter by repository path

## Commands

### Get All Workflows
```powershell
curl http://localhost:5300/api/developer/workflows
```

### Filter by Repository Path
```powershell
curl "http://localhost:5300/api/developer/workflows?repositoryPath=C:\work\Brightsword"
```

## Response

Returns an array of workflow objects:
```json
[
  {
    "id": "guid",
    "title": "Workflow Title",
    "description": "Description",
    "repositoryPath": "C:\\work\\Repo",
    "status": "Pending",
    "createdAt": "2025-12-04T..."
  }
]
```
