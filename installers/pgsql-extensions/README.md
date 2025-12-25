# PostgreSQL Extensions

This directory contains pre-compiled PostgreSQL extensions for bundling with the installer.

## Required Files for pgvector

To enable vector search, you need to place the following files here:

- `vector.dll` - The pgvector extension binary (compiled for Windows x64, PostgreSQL 16)
- `vector.control` - Extension control file
- `vector--0.7.0.sql` - SQL installation script (version may vary)

## How to Obtain pgvector for Windows

### Option 1: Download Pre-built (Recommended)

Download from the pgvector releases page:
https://github.com/pgvector/pgvector/releases

Look for Windows x64 builds compatible with PostgreSQL 16.

### Option 2: Build from Source

1. Install Visual Studio Build Tools
2. Install PostgreSQL 16 development headers
3. Clone pgvector: `git clone https://github.com/pgvector/pgvector.git`
4. Build: `nmake /F Makefile.win`
5. Copy the resulting files here

## Without pgvector

If pgvector is not available, Aura will still work but vector similarity search (RAG) will be disabled. You can use the API with keyword-based search as a fallback.
