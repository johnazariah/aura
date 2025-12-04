# RAG Index - Delete

Delete content from the RAG index.

## Quick Method (Recommended)

```powershell
# Clear entire index (with confirmation)
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Clear

# Clear without confirmation
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Clear -Force
```

## API Endpoints

### Delete All Content
```
DELETE http://localhost:5300/api/rag
```

### Delete Specific Content by ID
```
DELETE http://localhost:5300/api/rag/{contentId}
```

## Commands

### Delete Entire RAG Index (Clear All)
```powershell
curl -X DELETE http://localhost:5300/api/rag
```

Or with Invoke-RestMethod:
```powershell
Invoke-RestMethod -Uri "http://localhost:5300/api/rag" -Method Delete
```

### Delete Specific Content
```powershell
curl -X DELETE http://localhost:5300/api/rag/CONTENT-ID-HERE
```

## Verification

After deletion, verify the index is empty:
```powershell
curl http://localhost:5300/api/rag/stats
```

Should show zero chunks and files.

## Note

To re-index after clearing:
```powershell
$body = @{
    path = "C:\work\Brightsword"
    recursive = $true
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5300/api/rag/index/directory" -Method Post -Body $body -ContentType "application/json"
```
