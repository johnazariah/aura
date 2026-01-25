# Index Your Codebase

Indexing teaches Aura about your code structure.

## What Gets Indexed

- **Functions & Methods** - Names, signatures, docstrings
- **Classes & Types** - Definitions, inheritance
- **Files & Structure** - Project organization
- **Relationships** - How code connects

## Supported Languages

| Language | Indexing Level |
|----------|----------------|
| C# | Full (Roslyn) |
| TypeScript/JS | Full (TreeSitter) |
| Python | Full (TreeSitter) |
| Go, Rust, Java | Full (TreeSitter) |

## How to Index

1. Open your project folder
2. Click **Index Workspace** button
3. Wait for indexing to complete (watch the progress)

## Re-Index When

- After pulling significant changes
- After adding new files
- When chat seems outdated
