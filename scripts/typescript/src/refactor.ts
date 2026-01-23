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

import { Project, Node, SyntaxKind, SourceFile, ts } from "ts-morph";
import * as path from "path";

interface RefactoringResult {
  success: boolean;
  error?: string;
  errorType?: string;
  preview?: boolean;
  changedFiles?: string[];
  description?: string;
}

interface ReferenceLocation {
  file: string;
  line: number;
  column: number;
  text: string;
}

interface FindReferencesResult {
  success: boolean;
  error?: string;
  references?: ReferenceLocation[];
  count?: number;
}

interface FindDefinitionResult {
  success: boolean;
  error?: string;
  found?: boolean;
  filePath?: string;
  line?: number;
  column?: number;
  offset?: number;
  message?: string;
}

function getProject(projectPath: string): Project {
  const tsConfigPath = path.join(projectPath, "tsconfig.json");
  
  try {
    return new Project({
      tsConfigFilePath: tsConfigPath,
    });
  } catch {
    // Fallback: create project without tsconfig
    return new Project({
      compilerOptions: {
        target: ts.ScriptTarget.ES2022,
        module: ts.ModuleKind.NodeNext,
        strict: true,
        esModuleInterop: true,
        skipLibCheck: true,
        allowJs: true,
      },
    });
  }
}

function findNodeAtOffset(sourceFile: SourceFile, offset: number): Node | undefined {
  return sourceFile.getDescendantAtPos(offset);
}

function renameSymbol(
  projectPath: string,
  filePath: string,
  offset: number,
  newName: string,
  preview: boolean = false
): RefactoringResult {
  try {
    const project = getProject(projectPath);
    
    // Add the file if not in project
    const absolutePath = path.isAbsolute(filePath) ? filePath : path.join(projectPath, filePath);
    let sourceFile = project.getSourceFile(absolutePath);
    if (!sourceFile) {
      sourceFile = project.addSourceFileAtPath(absolutePath);
    }

    const node = findNodeAtOffset(sourceFile, offset);
    if (!node) {
      return {
        success: false,
        error: `No symbol found at offset ${offset}`,
        errorType: "SymbolNotFound",
      };
    }

    // Find the identifier node
    let identifier = node;
    if (!Node.isIdentifier(identifier)) {
      identifier = node.getFirstAncestorByKind(SyntaxKind.Identifier) ?? node;
    }

    if (!Node.isIdentifier(identifier) && !Node.isStringLiteral(identifier)) {
      return {
        success: false,
        error: `Node at offset ${offset} is not a renameable symbol (found ${node.getKindName()})`,
        errorType: "NotRenameable",
      };
    }

    // Perform the rename - ts-morph handles finding all references automatically
    if (Node.isIdentifier(identifier)) {
      identifier.rename(newName);
    }

    if (preview) {
      // Get changed files but don't save
      const changedFiles = project.getSourceFiles()
        .filter(sf => !sf.isSaved())
        .map(sf => sf.getFilePath());

      return {
        success: true,
        preview: true,
        changedFiles,
        description: `Rename to '${newName}'`,
      };
    } else {
      // Save all changes
      project.saveSync();
      
      const changedFiles = project.getSourceFiles()
        .map(sf => sf.getFilePath());

      return {
        success: true,
        preview: false,
        changedFiles,
        description: `Renamed to '${newName}'`,
      };
    }
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
      errorType: e instanceof Error ? e.constructor.name : "UnknownError",
    };
  }
}

function extractFunction(
  projectPath: string,
  filePath: string,
  startOffset: number,
  endOffset: number,
  newName: string,
  preview: boolean = false
): RefactoringResult {
  try {
    const project = getProject(projectPath);
    
    const absolutePath = path.isAbsolute(filePath) ? filePath : path.join(projectPath, filePath);
    let sourceFile = project.getSourceFile(absolutePath);
    if (!sourceFile) {
      sourceFile = project.addSourceFileAtPath(absolutePath);
    }

    // Get the text to extract
    const fullText = sourceFile.getFullText();
    const extractedText = fullText.substring(startOffset, endOffset);

    // Find the containing function/method to determine context
    const startNode = findNodeAtOffset(sourceFile, startOffset);
    if (!startNode) {
      return {
        success: false,
        error: `No code found at offset ${startOffset}`,
        errorType: "CodeNotFound",
      };
    }

    // Find containing function or class
    const containingFunction = startNode.getFirstAncestor(node => 
      Node.isFunctionDeclaration(node) || 
      Node.isMethodDeclaration(node) || 
      Node.isArrowFunction(node) ||
      Node.isFunctionExpression(node)
    );

    // For now, we'll do a simple extraction by creating a new function
    // and replacing the selected code with a call to it
    // Note: A full implementation would analyze variables, parameters, return values
    
    // Find a good insertion point (before the containing function or at module level)
    const insertPosition = containingFunction 
      ? containingFunction.getStart()
      : sourceFile.getStatements()[0]?.getStart() ?? 0;

    // Create the new function
    const newFunction = `function ${newName}() {\n  ${extractedText.trim()}\n}\n\n`;
    
    // Insert the new function
    sourceFile.insertText(insertPosition, newFunction);
    
    // Replace the original code with a function call
    // Adjust offsets since we inserted text
    const adjustedStart = startOffset + newFunction.length;
    const adjustedEnd = endOffset + newFunction.length;
    sourceFile.replaceText([adjustedStart, adjustedEnd], `${newName}()`);

    if (preview) {
      return {
        success: true,
        preview: true,
        changedFiles: [sourceFile.getFilePath()],
        description: `Extract function '${newName}'`,
      };
    } else {
      project.saveSync();
      return {
        success: true,
        preview: false,
        changedFiles: [sourceFile.getFilePath()],
        description: `Extracted function '${newName}'`,
      };
    }
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
      errorType: e instanceof Error ? e.constructor.name : "UnknownError",
    };
  }
}

function extractVariable(
  projectPath: string,
  filePath: string,
  startOffset: number,
  endOffset: number,
  newName: string,
  preview: boolean = false
): RefactoringResult {
  try {
    const project = getProject(projectPath);
    
    const absolutePath = path.isAbsolute(filePath) ? filePath : path.join(projectPath, filePath);
    let sourceFile = project.getSourceFile(absolutePath);
    if (!sourceFile) {
      sourceFile = project.addSourceFileAtPath(absolutePath);
    }

    const fullText = sourceFile.getFullText();
    const extractedText = fullText.substring(startOffset, endOffset);

    // Find the statement containing the selection
    const startNode = findNodeAtOffset(sourceFile, startOffset);
    if (!startNode) {
      return {
        success: false,
        error: `No code found at offset ${startOffset}`,
        errorType: "CodeNotFound",
      };
    }

    // Find containing statement
    const containingStatement = startNode.getFirstAncestor(node => Node.isStatement(node));
    const insertPosition = containingStatement?.getStart() ?? startOffset;

    // Create variable declaration
    const variableDecl = `const ${newName} = ${extractedText.trim()};\n`;
    
    // Insert the variable declaration
    sourceFile.insertText(insertPosition, variableDecl);
    
    // Replace the original expression with the variable name
    const adjustedStart = startOffset + variableDecl.length;
    const adjustedEnd = endOffset + variableDecl.length;
    sourceFile.replaceText([adjustedStart, adjustedEnd], newName);

    if (preview) {
      return {
        success: true,
        preview: true,
        changedFiles: [sourceFile.getFilePath()],
        description: `Extract variable '${newName}'`,
      };
    } else {
      project.saveSync();
      return {
        success: true,
        preview: false,
        changedFiles: [sourceFile.getFilePath()],
        description: `Extracted variable '${newName}'`,
      };
    }
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
      errorType: e instanceof Error ? e.constructor.name : "UnknownError",
    };
  }
}

function findReferences(
  projectPath: string,
  filePath: string,
  offset: number
): FindReferencesResult {
  try {
    const project = getProject(projectPath);
    
    const absolutePath = path.isAbsolute(filePath) ? filePath : path.join(projectPath, filePath);
    let sourceFile = project.getSourceFile(absolutePath);
    if (!sourceFile) {
      sourceFile = project.addSourceFileAtPath(absolutePath);
    }

    const node = findNodeAtOffset(sourceFile, offset);
    if (!node) {
      return {
        success: false,
        error: `No symbol found at offset ${offset}`,
      };
    }

    // Find identifier
    let identifier = node;
    if (!Node.isIdentifier(identifier)) {
      const parent = node.getFirstAncestorByKind(SyntaxKind.Identifier);
      if (parent) identifier = parent;
    }

    if (!Node.isIdentifier(identifier)) {
      return {
        success: false,
        error: `Node at offset ${offset} is not a symbol`,
      };
    }

    // Get all references
    const refs = identifier.findReferencesAsNodes();
    
    const references: ReferenceLocation[] = refs.map(ref => {
      const sf = ref.getSourceFile();
      const lineAndCol = sf.getLineAndColumnAtPos(ref.getStart());
      return {
        file: sf.getFilePath(),
        line: lineAndCol.line,
        column: lineAndCol.column,
        text: ref.getText(),
      };
    });

    return {
      success: true,
      references,
      count: references.length,
    };
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
    };
  }
}

function findDefinition(
  projectPath: string,
  filePath: string,
  offset: number
): FindDefinitionResult {
  try {
    const project = getProject(projectPath);
    
    const absolutePath = path.isAbsolute(filePath) ? filePath : path.join(projectPath, filePath);
    let sourceFile = project.getSourceFile(absolutePath);
    if (!sourceFile) {
      sourceFile = project.addSourceFileAtPath(absolutePath);
    }

    const node = findNodeAtOffset(sourceFile, offset);
    if (!node) {
      return {
        success: true,
        found: false,
        message: `No symbol found at offset ${offset}`,
      };
    }

    // Find identifier
    let identifier = node;
    if (!Node.isIdentifier(identifier)) {
      const parent = node.getFirstAncestorByKind(SyntaxKind.Identifier);
      if (parent) identifier = parent;
    }

    if (!Node.isIdentifier(identifier)) {
      return {
        success: true,
        found: false,
        message: `Node at offset ${offset} is not a symbol`,
      };
    }

    // Get definition
    const definitions = identifier.getDefinitionNodes();
    if (definitions.length === 0) {
      return {
        success: true,
        found: false,
        message: "No definition found",
      };
    }

    const def = definitions[0];
    const defSourceFile = def.getSourceFile();
    const lineAndCol = defSourceFile.getLineAndColumnAtPos(def.getStart());

    return {
      success: true,
      found: true,
      filePath: defSourceFile.getFilePath(),
      line: lineAndCol.line,
      column: lineAndCol.column,
      offset: def.getStart(),
    };
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
    };
  }
}

// CLI argument parsing
function parseArgs(): { command: string; args: Record<string, string | boolean> } {
  const args = process.argv.slice(2);
  const command = args[0] || "help";
  const parsed: Record<string, string | boolean> = {};

  for (let i = 1; i < args.length; i++) {
    const arg = args[i];
    if (arg.startsWith("--")) {
      const key = arg.slice(2);
      const nextArg = args[i + 1];
      if (nextArg && !nextArg.startsWith("--")) {
        parsed[key] = nextArg;
        i++;
      } else {
        parsed[key] = true;
      }
    }
  }

  return { command, args: parsed };
}

function main() {
  const { command, args } = parseArgs();

  let result: RefactoringResult | FindReferencesResult | FindDefinitionResult;

  switch (command) {
    case "rename":
      result = renameSymbol(
        args.project as string,
        args.file as string,
        parseInt(args.offset as string, 10),
        args["new-name"] as string,
        args.preview === true
      );
      break;

    case "extract-function":
      result = extractFunction(
        args.project as string,
        args.file as string,
        parseInt(args["start-offset"] as string, 10),
        parseInt(args["end-offset"] as string, 10),
        args["new-name"] as string,
        args.preview === true
      );
      break;

    case "extract-variable":
      result = extractVariable(
        args.project as string,
        args.file as string,
        parseInt(args["start-offset"] as string, 10),
        parseInt(args["end-offset"] as string, 10),
        args["new-name"] as string,
        args.preview === true
      );
      break;

    case "find-references":
      result = findReferences(
        args.project as string,
        args.file as string,
        parseInt(args.offset as string, 10)
      );
      break;

    case "find-definition":
      result = findDefinition(
        args.project as string,
        args.file as string,
        parseInt(args.offset as string, 10)
      );
      break;

    case "help":
    default:
      console.log(`TypeScript/JavaScript Refactoring Tool

Commands:
  rename            Rename a symbol at the given offset
  extract-function  Extract code into a new function
  extract-variable  Extract expression into a variable
  find-references   Find all references to a symbol
  find-definition   Find the definition of a symbol

Options:
  --project         Path to the project root (required)
  --file            Path to the file (required)
  --offset          Character offset for rename/find operations
  --start-offset    Start offset for extract operations
  --end-offset      End offset for extract operations
  --new-name        New name for rename/extract operations
  --preview         Show changes without applying them
`);
      process.exit(0);
  }

  console.log(JSON.stringify(result, null, 2));
}

main();
