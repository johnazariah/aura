# LSP-Based Refactoring Framework

**Status:** 💭 Unplanned
**Priority:** Low
**Created:** 2026-01-30
**Dependencies:** TypeScript Refactoring (should be done first)

## Overview

Generic Language Server Protocol (LSP) integration for refactoring operations in languages without dedicated libraries (Go, Rust, Java, etc.).

## Rationale for Deferral

1. **No immediate demand** - No user requests for Go/Rust/Java refactoring
2. **Complexity** - LSP server lifecycle management is non-trivial
3. **TypeScript first** - Prove the multi-language model with ts-morph before adding more

## Languages & Servers

| Language | Server | Installation |
|----------|--------|--------------|
| Go | gopls | `go install golang.org/x/tools/gopls@latest` |
| Rust | rust-analyzer | `rustup component add rust-analyzer` |
| Java | jdtls | Eclipse JDT Language Server |

## LSP Operations

Standard LSP refactoring operations:
- `textDocument/rename` - Rename symbol
- `textDocument/references` - Find all references  
- `textDocument/definition` - Go to definition
- `workspace/applyEdit` - Apply multi-file edits

## Estimated Effort

- Generic LSP client: 4-5 days
- Per-language integration: 2-3 days each
- Total: 2+ weeks

## Prerequisites

Before implementing:
1. TypeScript support proven and stable
2. Clear user demand for specific language
3. Decision on bundling vs. requiring user-installed servers

## References

- [LSP Specification](https://microsoft.github.io/language-server-protocol/)
- [gopls](https://pkg.go.dev/golang.org/x/tools/gopls)
- [rust-analyzer](https://rust-analyzer.github.io/)
