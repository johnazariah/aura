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

import { Project, Node, SyntaxKind, SourceFile, ts, ClassDeclaration, InterfaceDeclaration, TypeAliasDeclaration, EnumDeclaration } from "ts-morph";
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

interface MemberInfo {
  name: string;
  kind: string;
  type?: string;
  visibility?: string;
  isStatic: boolean;
  isAsync: boolean;
  line: number;
}

interface InspectTypeResult {
  success: boolean;
  error?: string;
  typeName?: string;
  kind?: string;
  filePath?: string;
  line?: number;
  members?: MemberInfo[];
}

interface TypeInfo {
  name: string;
  kind: string;
  filePath: string;
  line: number;
  isExported: boolean;
  memberCount: number;
}

interface ListTypesResult {
  success: boolean;
  error?: string;
  types?: TypeInfo[];
  count?: number;
}

interface CallerLocation {
  file: string;
  line: number;
  column: number;
  name: string;
  kind: string;
  text: string;
}

interface FindCallersResult {
  success: boolean;
  error?: string;
  callers?: CallerLocation[];
  count?: number;
}

interface ImplementationLocation {
  name: string;
  kind: string;
  file: string;
  line: number;
  column: number;
}

interface FindImplementationsResult {
  success: boolean;
  error?: string;
  implementations?: ImplementationLocation[];
  count?: number;
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

function getVisibility(node: Node): string {
  if (Node.isModifierable(node)) {
    if (node.hasModifier(SyntaxKind.PrivateKeyword)) return "private";
    if (node.hasModifier(SyntaxKind.ProtectedKeyword)) return "protected";
  }
  return "public";
}

function getMemberCount(
  node: ClassDeclaration | InterfaceDeclaration | EnumDeclaration | TypeAliasDeclaration,
): number {
  if (Node.isClassDeclaration(node)) {
    return (
      node.getProperties().length +
      node.getMethods().length +
      node.getGetAccessors().length +
      node.getSetAccessors().length
    );
  }
  if (Node.isInterfaceDeclaration(node)) {
    return node.getProperties().length + node.getMethods().length;
  }
  if (Node.isEnumDeclaration(node)) {
    return node.getMembers().length;
  }
  return 0;
}

function inspectType(
  projectPath: string,
  typeName: string,
  filePath?: string,
): InspectTypeResult {
  try {
    const project = getProject(projectPath);

    if (filePath) {
      const absolutePath = path.isAbsolute(filePath)
        ? filePath
        : path.join(projectPath, filePath);
      if (!project.getSourceFile(absolutePath)) {
        project.addSourceFileAtPath(absolutePath);
      }
    }

    const sourceFiles = filePath
      ? [
          project.getSourceFile(
            path.isAbsolute(filePath)
              ? filePath
              : path.join(projectPath, filePath),
          )!,
        ].filter(Boolean)
      : project
          .getSourceFiles()
          .filter((sf) => !sf.getFilePath().includes("node_modules"));

    for (const sf of sourceFiles) {
      // Search classes
      for (const cls of sf.getClasses()) {
        if (cls.getName() === typeName) {
          const members: MemberInfo[] = [];

          for (const ctor of cls.getConstructors()) {
            members.push({
              name: "constructor",
              kind: "constructor",
              visibility: getVisibility(ctor),
              isStatic: false,
              isAsync: false,
              line: ctor.getStartLineNumber(),
            });
          }

          for (const prop of cls.getProperties()) {
            members.push({
              name: prop.getName(),
              kind: "property",
              type: prop.getType().getText(prop),
              visibility: getVisibility(prop),
              isStatic: prop.isStatic(),
              isAsync: false,
              line: prop.getStartLineNumber(),
            });
          }

          for (const method of cls.getMethods()) {
            members.push({
              name: method.getName(),
              kind: "method",
              type: method.getReturnType().getText(method),
              visibility: getVisibility(method),
              isStatic: method.isStatic(),
              isAsync: method.isAsync(),
              line: method.getStartLineNumber(),
            });
          }

          for (const getter of cls.getGetAccessors()) {
            members.push({
              name: getter.getName(),
              kind: "getter",
              type: getter.getReturnType().getText(getter),
              visibility: getVisibility(getter),
              isStatic: getter.isStatic(),
              isAsync: false,
              line: getter.getStartLineNumber(),
            });
          }

          for (const setter of cls.getSetAccessors()) {
            members.push({
              name: setter.getName(),
              kind: "setter",
              visibility: getVisibility(setter),
              isStatic: setter.isStatic(),
              isAsync: false,
              line: setter.getStartLineNumber(),
            });
          }

          return {
            success: true,
            typeName: cls.getName(),
            kind: "class",
            filePath: sf.getFilePath(),
            line: cls.getStartLineNumber(),
            members,
          };
        }
      }

      // Search interfaces
      for (const iface of sf.getInterfaces()) {
        if (iface.getName() === typeName) {
          const members: MemberInfo[] = [];

          for (const prop of iface.getProperties()) {
            members.push({
              name: prop.getName(),
              kind: "property",
              type: prop.getType().getText(prop),
              visibility: "public",
              isStatic: false,
              isAsync: false,
              line: prop.getStartLineNumber(),
            });
          }

          for (const method of iface.getMethods()) {
            members.push({
              name: method.getName(),
              kind: "method",
              type: method.getReturnType().getText(method),
              visibility: "public",
              isStatic: false,
              isAsync: false,
              line: method.getStartLineNumber(),
            });
          }

          return {
            success: true,
            typeName: iface.getName(),
            kind: "interface",
            filePath: sf.getFilePath(),
            line: iface.getStartLineNumber(),
            members,
          };
        }
      }

      // Search enums
      for (const enumDecl of sf.getEnums()) {
        if (enumDecl.getName() === typeName) {
          const members: MemberInfo[] = enumDecl.getMembers().map((m) => ({
            name: m.getName(),
            kind: "enum-member",
            type: m.getValue()?.toString(),
            visibility: "public",
            isStatic: false,
            isAsync: false,
            line: m.getStartLineNumber(),
          }));

          return {
            success: true,
            typeName: enumDecl.getName(),
            kind: "enum",
            filePath: sf.getFilePath(),
            line: enumDecl.getStartLineNumber(),
            members,
          };
        }
      }

      // Search type aliases
      for (const typeAlias of sf.getTypeAliases()) {
        if (typeAlias.getName() === typeName) {
          return {
            success: true,
            typeName: typeAlias.getName(),
            kind: "type",
            filePath: sf.getFilePath(),
            line: typeAlias.getStartLineNumber(),
            members: [],
          };
        }
      }
    }

    return {
      success: false,
      error: `Type '${typeName}' not found in project`,
    };
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
    };
  }
}

function listTypes(
  projectPath: string,
  nameFilter?: string,
): ListTypesResult {
  try {
    const project = getProject(projectPath);
    const types: TypeInfo[] = [];

    for (const sf of project.getSourceFiles()) {
      const fp = sf.getFilePath();
      if (fp.includes("node_modules") || fp.endsWith(".d.ts")) continue;

      for (const cls of sf.getClasses()) {
        const name = cls.getName();
        if (!name) continue;
        if (
          nameFilter &&
          !name.toLowerCase().includes(nameFilter.toLowerCase())
        )
          continue;
        types.push({
          name,
          kind: "class",
          filePath: fp,
          line: cls.getStartLineNumber(),
          isExported: cls.isExported(),
          memberCount: getMemberCount(cls),
        });
      }

      for (const iface of sf.getInterfaces()) {
        const name = iface.getName();
        if (
          nameFilter &&
          !name.toLowerCase().includes(nameFilter.toLowerCase())
        )
          continue;
        types.push({
          name,
          kind: "interface",
          filePath: fp,
          line: iface.getStartLineNumber(),
          isExported: iface.isExported(),
          memberCount: getMemberCount(iface),
        });
      }

      for (const enumDecl of sf.getEnums()) {
        const name = enumDecl.getName();
        if (
          nameFilter &&
          !name.toLowerCase().includes(nameFilter.toLowerCase())
        )
          continue;
        types.push({
          name,
          kind: "enum",
          filePath: fp,
          line: enumDecl.getStartLineNumber(),
          isExported: enumDecl.isExported(),
          memberCount: getMemberCount(enumDecl),
        });
      }

      for (const typeAlias of sf.getTypeAliases()) {
        const name = typeAlias.getName();
        if (
          nameFilter &&
          !name.toLowerCase().includes(nameFilter.toLowerCase())
        )
          continue;
        types.push({
          name,
          kind: "type",
          filePath: fp,
          line: typeAlias.getStartLineNumber(),
          isExported: typeAlias.isExported(),
          memberCount: 0,
        });
      }
    }

    return {
      success: true,
      types,
      count: types.length,
    };
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
    };
  }
}

function findCallers(
  projectPath: string,
  filePath: string,
  offset: number,
): FindCallersResult {
  try {
    const project = getProject(projectPath);

    const absolutePath = path.isAbsolute(filePath)
      ? filePath
      : path.join(projectPath, filePath);
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

    // Get the definition nodes for this symbol
    const definitions = identifier.getDefinitionNodes();
    const targetSymbolName = identifier.getText();

    // Find all references, then filter to call sites
    const refs = identifier.findReferencesAsNodes();
    const callers: CallerLocation[] = [];
    const seen = new Set<string>();

    for (const ref of refs) {
      // Skip the definition itself
      const refFile = ref.getSourceFile().getFilePath();
      const refStart = ref.getStart();
      const isDefinition = definitions.some(
        (d) =>
          d.getSourceFile().getFilePath() === refFile &&
          Math.abs(d.getStart() - refStart) < targetSymbolName.length + 5,
      );
      if (isDefinition) continue;

      // Find the containing function/method/class for this reference
      const containingFunc = ref.getFirstAncestor(
        (n) =>
          Node.isFunctionDeclaration(n) ||
          Node.isMethodDeclaration(n) ||
          Node.isArrowFunction(n) ||
          Node.isFunctionExpression(n) ||
          Node.isConstructorDeclaration(n),
      );

      let callerName = "<module>";
      let callerKind = "module";

      if (containingFunc) {
        if (Node.isConstructorDeclaration(containingFunc)) {
          const parentClass = containingFunc.getFirstAncestorByKind(
            SyntaxKind.ClassDeclaration,
          );
          callerName = parentClass?.getName()
            ? `${parentClass.getName()}.constructor`
            : "constructor";
          callerKind = "constructor";
        } else if (Node.isMethodDeclaration(containingFunc)) {
          const parentClass = containingFunc.getFirstAncestorByKind(
            SyntaxKind.ClassDeclaration,
          );
          callerName = parentClass?.getName()
            ? `${parentClass.getName()}.${containingFunc.getName()}`
            : containingFunc.getName();
          callerKind = "method";
        } else if (Node.isFunctionDeclaration(containingFunc)) {
          callerName = containingFunc.getName() ?? "<anonymous>";
          callerKind = "function";
        } else {
          // Arrow function or function expression — try to find the variable it's assigned to
          const varDecl = containingFunc.getFirstAncestorByKind(
            SyntaxKind.VariableDeclaration,
          );
          callerName = varDecl?.getName() ?? "<anonymous>";
          callerKind = "function";
        }
      }

      const sf = ref.getSourceFile();
      const lineAndCol = sf.getLineAndColumnAtPos(refStart);
      const key = `${refFile}:${lineAndCol.line}:${callerName}`;
      if (seen.has(key)) continue;
      seen.add(key);

      // Get the line text for context
      const lineText = sf
        .getFullText()
        .split("\n")
        [lineAndCol.line - 1]?.trim() ?? "";

      callers.push({
        file: refFile,
        line: lineAndCol.line,
        column: lineAndCol.column,
        name: callerName,
        kind: callerKind,
        text: lineText.length > 200 ? lineText.substring(0, 200) + "..." : lineText,
      });
    }

    return {
      success: true,
      callers,
      count: callers.length,
    };
  } catch (e) {
    return {
      success: false,
      error: e instanceof Error ? e.message : String(e),
    };
  }
}

function findImplementations(
  projectPath: string,
  filePath: string,
  offset: number,
): FindImplementationsResult {
  try {
    const project = getProject(projectPath);

    const absolutePath = path.isAbsolute(filePath)
      ? filePath
      : path.join(projectPath, filePath);
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

    const definitions = identifier.getDefinitionNodes();
    const implementations: ImplementationLocation[] = [];

    for (const def of definitions) {
      // If the definition is an interface, find classes that implement it
      if (Node.isInterfaceDeclaration(def)) {
        const ifaceName = def.getName();
        const allSourceFiles = project
          .getSourceFiles()
          .filter((sf) => !sf.getFilePath().includes("node_modules"));

        for (const sf of allSourceFiles) {
          for (const cls of sf.getClasses()) {
            const implementsInterfaces = cls.getImplements();
            const extendsExpr = cls.getExtends();

            const implementsTarget = implementsInterfaces.some(
              (impl) => impl.getText() === ifaceName,
            );
            const extendsTarget =
              extendsExpr?.getText() === ifaceName;

            if (implementsTarget || extendsTarget) {
              const lineAndCol = sf.getLineAndColumnAtPos(cls.getStart());
              implementations.push({
                name: cls.getName() ?? "<anonymous>",
                kind: "class",
                file: sf.getFilePath(),
                line: lineAndCol.line,
                column: lineAndCol.column,
              });
            }
          }

          // Also check interfaces that extend this interface
          for (const iface of sf.getInterfaces()) {
            if (iface === def) continue;
            const extendsExprs = iface.getExtends();
            const extendsTarget = extendsExprs.some(
              (ext) => ext.getText() === ifaceName,
            );
            if (extendsTarget) {
              const lineAndCol = sf.getLineAndColumnAtPos(iface.getStart());
              implementations.push({
                name: iface.getName(),
                kind: "interface",
                file: sf.getFilePath(),
                line: lineAndCol.line,
                column: lineAndCol.column,
              });
            }
          }
        }
      }

      // If the definition is an abstract class, find classes that extend it
      if (Node.isClassDeclaration(def) && def.isAbstract()) {
        const className = def.getName();
        const allSourceFiles = project
          .getSourceFiles()
          .filter((sf) => !sf.getFilePath().includes("node_modules"));

        for (const sf of allSourceFiles) {
          for (const cls of sf.getClasses()) {
            if (cls === def) continue;
            const extendsExpr = cls.getExtends();
            if (extendsExpr?.getText() === className) {
              const lineAndCol = sf.getLineAndColumnAtPos(cls.getStart());
              implementations.push({
                name: cls.getName() ?? "<anonymous>",
                kind: "class",
                file: sf.getFilePath(),
                line: lineAndCol.line,
                column: lineAndCol.column,
              });
            }
          }
        }
      }

      // If the definition is an abstract/overridable method, find overrides
      if (Node.isMethodDeclaration(def)) {
        const parentClass = def.getFirstAncestorByKind(
          SyntaxKind.ClassDeclaration,
        );
        if (parentClass) {
          const methodName = def.getName();
          const className = parentClass.getName();
          const allSourceFiles = project
            .getSourceFiles()
            .filter((sf) => !sf.getFilePath().includes("node_modules"));

          for (const sf of allSourceFiles) {
            for (const cls of sf.getClasses()) {
              if (cls === parentClass) continue;
              const extendsExpr = cls.getExtends();
              if (extendsExpr?.getText() === className) {
                const overrideMethod = cls.getMethod(methodName);
                if (overrideMethod) {
                  const lineAndCol = sf.getLineAndColumnAtPos(
                    overrideMethod.getStart(),
                  );
                  implementations.push({
                    name: `${cls.getName()}.${methodName}`,
                    kind: "method",
                    file: sf.getFilePath(),
                    line: lineAndCol.line,
                    column: lineAndCol.column,
                  });
                }
              }
            }
          }
        }
      }
    }

    return {
      success: true,
      implementations,
      count: implementations.length,
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

  let result:
    | RefactoringResult
    | FindReferencesResult
    | FindDefinitionResult
    | InspectTypeResult
    | ListTypesResult
    | FindCallersResult
    | FindImplementationsResult;

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

    case "inspect-type":
      result = inspectType(
        args.project as string,
        args["type-name"] as string,
        args.file as string | undefined,
      );
      break;

    case "list-types":
      result = listTypes(
        args.project as string,
        args["name-filter"] as string | undefined,
      );
      break;

    case "find-callers":
      result = findCallers(
        args.project as string,
        args.file as string,
        parseInt(args.offset as string, 10),
      );
      break;

    case "find-implementations":
      result = findImplementations(
        args.project as string,
        args.file as string,
        parseInt(args.offset as string, 10),
      );
      break;

    case "help":
    default:
      console.log(`TypeScript/JavaScript Refactoring Tool

Commands:
  rename              Rename a symbol at the given offset
  extract-function    Extract code into a new function
  extract-variable    Extract expression into a variable
  find-references     Find all references to a symbol
  find-definition     Find the definition of a symbol
  find-callers        Find all callers of a function/method
  find-implementations Find implementations of an interface or abstract class
  inspect-type        Inspect a type's members (properties, methods, etc.)
  list-types          List all types in a project

Options:
  --project         Path to the project root (required)
  --file            Path to the file (required for most commands, optional for inspect-type)
  --offset          Character offset for rename/find operations
  --start-offset    Start offset for extract operations
  --end-offset      End offset for extract operations
  --new-name        New name for rename/extract operations
  --preview         Show changes without applying them
  --type-name       Type name for inspect-type
  --name-filter     Filter types by name (partial match) for list-types
`);
      process.exit(0);
  }

  console.log(JSON.stringify(result, null, 2));
}

main();
