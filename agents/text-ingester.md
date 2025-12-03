---
agentId: text-ingester
name: Text Ingester
description: Chunks text files by paragraphs or sections. Used for documentation, markdown, and plain text.
capabilities:
  - ingest:txt
  - ingest:md
  - ingest:rst
  - ingest:adoc
  - ingest:log
priority: 50
provider: native
model: none
temperature: 0
tags:
  - ingester
  - text
  - native
---

## Description

This is a native text ingester that chunks files by blank lines (paragraphs) or headers.
It does not use an LLM - it's purely rule-based.

For markdown files, it splits by headers (# ## ###).
For plain text, it splits by blank lines.

This agent is implemented in code as it doesn't need LLM intelligence.
The markdown file is here for documentation and capability registration.
