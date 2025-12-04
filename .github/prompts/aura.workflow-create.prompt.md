# Create Workflow

Create a new workflow in the Aura system.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-Workflows.ps1 -Action Create -Title "My Workflow" -Description "Description" -RepositoryPath "C:\work\Brightsword"
```

## API Endpoint

```
POST http://localhost:5300/api/developer/workflows
```

## Request Body

```json
{
  "title": "Workflow Title",
  "description": "Optional description of the workflow",
  "repositoryPath": "C:\\work\\YourRepo"
}
```

## Command

```powershell
$body = @{
    title = "Workflow Title"
    description = "Description of what needs to be done"
    repositoryPath = "C:\work\Brightsword"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/developer/workflows" -Method Post -Body $body -ContentType "application/json"
```

Or with curl:
```powershell
curl -X POST http://localhost:5300/api/developer/workflows -H "Content-Type: application/json" -d "{\"title\":\"My Workflow\",\"description\":\"Description\",\"repositoryPath\":\"C:\\\\work\\\\Brightsword\"}"
```

## Response

Returns the created workflow object with:
- `id`: GUID of the workflow
- `title`: Workflow title
- `description`: Workflow description
- `repositoryPath`: Associated repository path
- `status`: Initial status (typically "Pending")
- `createdAt`: Creation timestamp
