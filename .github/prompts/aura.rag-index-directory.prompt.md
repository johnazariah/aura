# RAG Index - Index Directory

Index a directory for RAG (Retrieval Augmented Generation) search.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Index -Path "C:\work\Brightsword"
```

## API Endpoint

```
POST http://localhost:5300/api/rag/index/directory
```

## Request Body

```json
{
  "path": "C:\\work\\YourRepo",
  "includePatterns": ["*.cs", "*.ts", "*.md"],
  "excludePatterns": ["**/bin/**", "**/node_modules/**"],
  "recursive": true
}
```

## Commands

### Index a Directory (Basic)
```powershell
$body = @{
    path = "C:\work\Brightsword"
    recursive = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/rag/index/directory" -Method Post -Body $body -ContentType "application/json"
```

### With curl
```powershell
curl -X POST http://localhost:5300/api/rag/index/directory -H "Content-Type: application/json" -d "{\"path\":\"C:\\\\work\\\\Brightsword\",\"recursive\":true}"
```

### With Include/Exclude Patterns
```powershell
$body = @{
    path = "C:\work\Brightsword"
    includePatterns = @("*.cs", "*.ts", "*.md")
    excludePatterns = @("**/bin/**", "**/obj/**", "**/node_modules/**")
    recursive = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/rag/index/directory" -Method Post -Body $body -ContentType "application/json"
```

## Background Indexing (Async)

For large directories, use background indexing:
```powershell
curl -X POST http://localhost:5300/api/index/background -H "Content-Type: application/json" -d "{\"workspacePath\":\"C:\\\\work\\\\Brightsword\"}"
```

Check status:
```powershell
curl http://localhost:5300/api/index/status
```

## Response

Returns indexing statistics:
```json
{
  "filesIndexed": 42,
  "chunksCreated": 156,
  "duration": "00:00:05.123"
}
```
