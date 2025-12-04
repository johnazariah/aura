# RAG Index - Get Stats

Get statistics about the RAG index.

## Quick Method (Recommended)

```powershell
# Global stats
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Stats

# Directory-specific stats
c:\work\aura\scripts\Manage-RagIndex.ps1 -Action Stats -Path "C:\work\Brightsword"
```

## API Endpoints

### Global Stats
```
GET http://localhost:5300/api/rag/stats
```

### Directory-Specific Stats
```
GET http://localhost:5300/api/rag/stats/directory?path={directoryPath}
```

## Commands

### Get Overall RAG Stats
```powershell
curl http://localhost:5300/api/rag/stats
```

### Check if Directory is Indexed
```powershell
curl "http://localhost:5300/api/rag/stats/directory?path=C:\work\Brightsword"
```

## Response Examples

### Global Stats
```json
{
  "totalChunks": 1234,
  "totalFiles": 89,
  "directories": ["C:\\work\\Repo1", "C:\\work\\Repo2"]
}
```

### Directory Stats
```json
{
  "isIndexed": true,
  "directoryPath": "C:\\work\\Brightsword",
  "chunkCount": 456,
  "fileCount": 32,
  "lastIndexedAt": "2025-12-04T10:30:00Z"
}
```

If not indexed:
```json
{
  "isIndexed": false,
  "directoryPath": "C:\\work\\Brightsword",
  "chunkCount": 0,
  "fileCount": 0,
  "lastIndexedAt": null
}
```
