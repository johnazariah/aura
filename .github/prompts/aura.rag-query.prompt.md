# RAG Index - Query

Query the RAG index for relevant content.

## Quick Method (Recommended)

```powershell
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Query -Query "How does authentication work?"

# With source filter
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Query -Query "database models" -Path "C:\work\Brightsword" -TopK 10
```

## API Endpoint

```
POST http://localhost:5300/api/rag/query
```

## Request Body

```json
{
  "query": "How does the authentication work?",
  "topK": 5,
  "sourcePath": "C:\\work\\Brightsword",
  "minimumScore": 0.5
}
```

## Commands

### Basic Query
```powershell
$body = @{
    query = "How does authentication work?"
    topK = 5
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/rag/query" -Method Post -Body $body -ContentType "application/json"
```

### Query with Source Filter
```powershell
$body = @{
    query = "database connection handling"
    topK = 10
    sourcePath = "C:\work\Brightsword"
    minimumScore = 0.6
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/rag/query" -Method Post -Body $body -ContentType "application/json"
```

### With curl
```powershell
curl -X POST http://localhost:5300/api/rag/query -H "Content-Type: application/json" -d "{\"query\":\"authentication\",\"topK\":5}"
```

## Response

Returns matching chunks with scores:
```json
{
  "results": [
    {
      "content": "The authentication service uses JWT tokens...",
      "source": "C:\\work\\Repo\\Services\\AuthService.cs",
      "score": 0.89,
      "metadata": {
        "startLine": 45,
        "endLine": 78
      }
    }
  ],
  "totalResults": 5,
  "queryTime": "00:00:00.234"
}
```
