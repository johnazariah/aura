# PostgreSQL Extensions

This directory documents the PostgreSQL extensions bundled with Aura.

## pgvector

The pgvector extension enables vector similarity search for RAG (Retrieval-Augmented Generation).

**Source:** https://github.com/andreiramani/pgvector_pgsql_windows/releases

The `Publish-Release.ps1` script automatically downloads prebuilt Windows binaries from the community-maintained repository above. The files are:

- `vector.dll` - The pgvector extension binary (compiled for Windows x64, PostgreSQL 16)
- `vector.control` - Extension control file  
- `vector--*.sql` - SQL installation/upgrade scripts

## Manual Override

If you need to use a different version, place the files here and modify `Publish-Release.ps1` to copy from this directory instead of downloading.

## Without pgvector

If pgvector is not available, Aura will still work but vector similarity search (RAG) will be disabled. You can use the API with keyword-based search as a fallback.
