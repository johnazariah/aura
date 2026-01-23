#!/usr/bin/env node
/**
 * TypeScript/JavaScript refactoring tool using ts-morph.
 * This script is called by Aura's TypeScriptRefactoringService to perform
 * refactoring operations on TypeScript/JavaScript codebases.
 *
 * Requirements:
 *     npm install ts-morph
 *
 * Usage:
 *     node refactor.js rename --project /path/to/project --file src/module.ts --offset 42 --new-name newName
 *     node refactor.js extract-function --project /path/to/project --file src/module.ts --start 10 --end 20 --new-name helperFunc
 *     node refactor.js find-references --project /path/to/project --file src/module.ts --offset 42
 */
export {};
