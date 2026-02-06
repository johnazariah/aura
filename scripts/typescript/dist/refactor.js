#!/usr/bin/env node
"use strict";
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
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
const ts_morph_1 = require("ts-morph");
const path = __importStar(require("path"));
function getProject(projectPath) {
    const tsConfigPath = path.join(projectPath, "tsconfig.json");
    try {
        return new ts_morph_1.Project({
            tsConfigFilePath: tsConfigPath,
        });
    }
    catch {
        // Fallback: create project without tsconfig
        return new ts_morph_1.Project({
            compilerOptions: {
                target: ts_morph_1.ts.ScriptTarget.ES2022,
                module: ts_morph_1.ts.ModuleKind.NodeNext,
                strict: true,
                esModuleInterop: true,
                skipLibCheck: true,
                allowJs: true,
            },
        });
    }
}
function findNodeAtOffset(sourceFile, offset) {
    return sourceFile.getDescendantAtPos(offset);
}
function renameSymbol(projectPath, filePath, offset, newName, preview = false) {
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
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            identifier = node.getFirstAncestorByKind(ts_morph_1.SyntaxKind.Identifier) ?? node;
        }
        if (!ts_morph_1.Node.isIdentifier(identifier) && !ts_morph_1.Node.isStringLiteral(identifier)) {
            return {
                success: false,
                error: `Node at offset ${offset} is not a renameable symbol (found ${node.getKindName()})`,
                errorType: "NotRenameable",
            };
        }
        // Perform the rename - ts-morph handles finding all references automatically
        if (ts_morph_1.Node.isIdentifier(identifier)) {
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
        }
        else {
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
            errorType: e instanceof Error ? e.constructor.name : "UnknownError",
        };
    }
}
function extractFunction(projectPath, filePath, startOffset, endOffset, newName, preview = false) {
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
        const containingFunction = startNode.getFirstAncestor(node => ts_morph_1.Node.isFunctionDeclaration(node) ||
            ts_morph_1.Node.isMethodDeclaration(node) ||
            ts_morph_1.Node.isArrowFunction(node) ||
            ts_morph_1.Node.isFunctionExpression(node));
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
        }
        else {
            project.saveSync();
            return {
                success: true,
                preview: false,
                changedFiles: [sourceFile.getFilePath()],
                description: `Extracted function '${newName}'`,
            };
        }
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
            errorType: e instanceof Error ? e.constructor.name : "UnknownError",
        };
    }
}
function extractVariable(projectPath, filePath, startOffset, endOffset, newName, preview = false) {
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
        const containingStatement = startNode.getFirstAncestor(node => ts_morph_1.Node.isStatement(node));
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
        }
        else {
            project.saveSync();
            return {
                success: true,
                preview: false,
                changedFiles: [sourceFile.getFilePath()],
                description: `Extracted variable '${newName}'`,
            };
        }
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
            errorType: e instanceof Error ? e.constructor.name : "UnknownError",
        };
    }
}
function findReferences(projectPath, filePath, offset) {
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
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            const parent = node.getFirstAncestorByKind(ts_morph_1.SyntaxKind.Identifier);
            if (parent)
                identifier = parent;
        }
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            return {
                success: false,
                error: `Node at offset ${offset} is not a symbol`,
            };
        }
        // Get all references
        const refs = identifier.findReferencesAsNodes();
        const references = refs.map(ref => {
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function findDefinition(projectPath, filePath, offset) {
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
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            const parent = node.getFirstAncestorByKind(ts_morph_1.SyntaxKind.Identifier);
            if (parent)
                identifier = parent;
        }
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function getVisibility(node) {
    if (ts_morph_1.Node.isModifierable(node)) {
        if (node.hasModifier(ts_morph_1.SyntaxKind.PrivateKeyword))
            return "private";
        if (node.hasModifier(ts_morph_1.SyntaxKind.ProtectedKeyword))
            return "protected";
    }
    return "public";
}
function getMemberCount(node) {
    if (ts_morph_1.Node.isClassDeclaration(node)) {
        return (node.getProperties().length +
            node.getMethods().length +
            node.getGetAccessors().length +
            node.getSetAccessors().length);
    }
    if (ts_morph_1.Node.isInterfaceDeclaration(node)) {
        return node.getProperties().length + node.getMethods().length;
    }
    if (ts_morph_1.Node.isEnumDeclaration(node)) {
        return node.getMembers().length;
    }
    return 0;
}
function inspectType(projectPath, typeName, filePath) {
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
                project.getSourceFile(path.isAbsolute(filePath)
                    ? filePath
                    : path.join(projectPath, filePath)),
            ].filter(Boolean)
            : project
                .getSourceFiles()
                .filter((sf) => !sf.getFilePath().includes("node_modules"));
        for (const sf of sourceFiles) {
            // Search classes
            for (const cls of sf.getClasses()) {
                if (cls.getName() === typeName) {
                    const members = [];
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
                    const members = [];
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
                    const members = enumDecl.getMembers().map((m) => ({
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function listTypes(projectPath, nameFilter) {
    try {
        const project = getProject(projectPath);
        const types = [];
        for (const sf of project.getSourceFiles()) {
            const fp = sf.getFilePath();
            if (fp.includes("node_modules") || fp.endsWith(".d.ts"))
                continue;
            for (const cls of sf.getClasses()) {
                const name = cls.getName();
                if (!name)
                    continue;
                if (nameFilter &&
                    !name.toLowerCase().includes(nameFilter.toLowerCase()))
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
                if (nameFilter &&
                    !name.toLowerCase().includes(nameFilter.toLowerCase()))
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
                if (nameFilter &&
                    !name.toLowerCase().includes(nameFilter.toLowerCase()))
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
                if (nameFilter &&
                    !name.toLowerCase().includes(nameFilter.toLowerCase()))
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function findCallers(projectPath, filePath, offset) {
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
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            const parent = node.getFirstAncestorByKind(ts_morph_1.SyntaxKind.Identifier);
            if (parent)
                identifier = parent;
        }
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
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
        const callers = [];
        const seen = new Set();
        for (const ref of refs) {
            // Skip the definition itself
            const refFile = ref.getSourceFile().getFilePath();
            const refStart = ref.getStart();
            const isDefinition = definitions.some((d) => d.getSourceFile().getFilePath() === refFile &&
                Math.abs(d.getStart() - refStart) < targetSymbolName.length + 5);
            if (isDefinition)
                continue;
            // Find the containing function/method/class for this reference
            const containingFunc = ref.getFirstAncestor((n) => ts_morph_1.Node.isFunctionDeclaration(n) ||
                ts_morph_1.Node.isMethodDeclaration(n) ||
                ts_morph_1.Node.isArrowFunction(n) ||
                ts_morph_1.Node.isFunctionExpression(n) ||
                ts_morph_1.Node.isConstructorDeclaration(n));
            let callerName = "<module>";
            let callerKind = "module";
            if (containingFunc) {
                if (ts_morph_1.Node.isConstructorDeclaration(containingFunc)) {
                    const parentClass = containingFunc.getFirstAncestorByKind(ts_morph_1.SyntaxKind.ClassDeclaration);
                    callerName = parentClass?.getName()
                        ? `${parentClass.getName()}.constructor`
                        : "constructor";
                    callerKind = "constructor";
                }
                else if (ts_morph_1.Node.isMethodDeclaration(containingFunc)) {
                    const parentClass = containingFunc.getFirstAncestorByKind(ts_morph_1.SyntaxKind.ClassDeclaration);
                    callerName = parentClass?.getName()
                        ? `${parentClass.getName()}.${containingFunc.getName()}`
                        : containingFunc.getName();
                    callerKind = "method";
                }
                else if (ts_morph_1.Node.isFunctionDeclaration(containingFunc)) {
                    callerName = containingFunc.getName() ?? "<anonymous>";
                    callerKind = "function";
                }
                else {
                    // Arrow function or function expression — try to find the variable it's assigned to
                    const varDecl = containingFunc.getFirstAncestorByKind(ts_morph_1.SyntaxKind.VariableDeclaration);
                    callerName = varDecl?.getName() ?? "<anonymous>";
                    callerKind = "function";
                }
            }
            const sf = ref.getSourceFile();
            const lineAndCol = sf.getLineAndColumnAtPos(refStart);
            const key = `${refFile}:${lineAndCol.line}:${callerName}`;
            if (seen.has(key))
                continue;
            seen.add(key);
            // Get the line text for context
            const lineText = sf
                .getFullText()
                .split("\n")[lineAndCol.line - 1]?.trim() ?? "";
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function findImplementations(projectPath, filePath, offset) {
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
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            const parent = node.getFirstAncestorByKind(ts_morph_1.SyntaxKind.Identifier);
            if (parent)
                identifier = parent;
        }
        if (!ts_morph_1.Node.isIdentifier(identifier)) {
            return {
                success: false,
                error: `Node at offset ${offset} is not a symbol`,
            };
        }
        const definitions = identifier.getDefinitionNodes();
        const implementations = [];
        for (const def of definitions) {
            // If the definition is an interface, find classes that implement it
            if (ts_morph_1.Node.isInterfaceDeclaration(def)) {
                const ifaceName = def.getName();
                const allSourceFiles = project
                    .getSourceFiles()
                    .filter((sf) => !sf.getFilePath().includes("node_modules"));
                for (const sf of allSourceFiles) {
                    for (const cls of sf.getClasses()) {
                        const implementsInterfaces = cls.getImplements();
                        const extendsExpr = cls.getExtends();
                        const implementsTarget = implementsInterfaces.some((impl) => impl.getText() === ifaceName);
                        const extendsTarget = extendsExpr?.getText() === ifaceName;
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
                        if (iface === def)
                            continue;
                        const extendsExprs = iface.getExtends();
                        const extendsTarget = extendsExprs.some((ext) => ext.getText() === ifaceName);
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
            if (ts_morph_1.Node.isClassDeclaration(def) && def.isAbstract()) {
                const className = def.getName();
                const allSourceFiles = project
                    .getSourceFiles()
                    .filter((sf) => !sf.getFilePath().includes("node_modules"));
                for (const sf of allSourceFiles) {
                    for (const cls of sf.getClasses()) {
                        if (cls === def)
                            continue;
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
            if (ts_morph_1.Node.isMethodDeclaration(def)) {
                const parentClass = def.getFirstAncestorByKind(ts_morph_1.SyntaxKind.ClassDeclaration);
                if (parentClass) {
                    const methodName = def.getName();
                    const className = parentClass.getName();
                    const allSourceFiles = project
                        .getSourceFiles()
                        .filter((sf) => !sf.getFilePath().includes("node_modules"));
                    for (const sf of allSourceFiles) {
                        for (const cls of sf.getClasses()) {
                            if (cls === parentClass)
                                continue;
                            const extendsExpr = cls.getExtends();
                            if (extendsExpr?.getText() === className) {
                                const overrideMethod = cls.getMethod(methodName);
                                if (overrideMethod) {
                                    const lineAndCol = sf.getLineAndColumnAtPos(overrideMethod.getStart());
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
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
function checkCompilation(projectPath) {
    try {
        const project = getProject(projectPath);
        const diagnostics = [];
        let errorCount = 0;
        let warningCount = 0;
        // Get pre-emit diagnostics (type errors, syntax errors, etc.)
        const preDiags = project.getPreEmitDiagnostics();
        for (const diag of preDiags) {
            const sf = diag.getSourceFile();
            // Skip diagnostics from node_modules
            if (sf && sf.getFilePath().includes("node_modules"))
                continue;
            const category = diag.getCategory();
            let severity;
            if (category === ts_morph_1.ts.DiagnosticCategory.Error) {
                severity = "error";
                errorCount++;
            }
            else if (category === ts_morph_1.ts.DiagnosticCategory.Warning) {
                severity = "warning";
                warningCount++;
            }
            else {
                continue; // Skip suggestions/messages
            }
            let filePath = "<unknown>";
            let line = 0;
            let column = 0;
            if (sf) {
                filePath = sf.getFilePath();
                const start = diag.getStart();
                if (start !== undefined) {
                    const lineAndCol = sf.getLineAndColumnAtPos(start);
                    line = lineAndCol.line;
                    column = lineAndCol.column;
                }
            }
            const code = `TS${diag.getCode()}`;
            const message = diag.getMessageText();
            const messageStr = typeof message === "string"
                ? message
                : ts_morph_1.ts.flattenDiagnosticMessageText(message.compilerObject, "\n");
            diagnostics.push({
                filePath,
                line,
                column,
                severity,
                code,
                message: messageStr,
            });
            // Limit to 50 diagnostics
            if (diagnostics.length >= 50)
                break;
        }
        return {
            success: true,
            compilationSucceeded: errorCount === 0,
            errorCount,
            warningCount,
            diagnostics,
        };
    }
    catch (e) {
        return {
            success: false,
            error: e instanceof Error ? e.message : String(e),
        };
    }
}
// CLI argument parsing
function parseArgs() {
    const args = process.argv.slice(2);
    const command = args[0] || "help";
    const parsed = {};
    for (let i = 1; i < args.length; i++) {
        const arg = args[i];
        if (arg.startsWith("--")) {
            const key = arg.slice(2);
            const nextArg = args[i + 1];
            if (nextArg && !nextArg.startsWith("--")) {
                parsed[key] = nextArg;
                i++;
            }
            else {
                parsed[key] = true;
            }
        }
    }
    return { command, args: parsed };
}
function main() {
    const { command, args } = parseArgs();
    let result;
    switch (command) {
        case "rename":
            result = renameSymbol(args.project, args.file, parseInt(args.offset, 10), args["new-name"], args.preview === true);
            break;
        case "extract-function":
            result = extractFunction(args.project, args.file, parseInt(args["start-offset"], 10), parseInt(args["end-offset"], 10), args["new-name"], args.preview === true);
            break;
        case "extract-variable":
            result = extractVariable(args.project, args.file, parseInt(args["start-offset"], 10), parseInt(args["end-offset"], 10), args["new-name"], args.preview === true);
            break;
        case "find-references":
            result = findReferences(args.project, args.file, parseInt(args.offset, 10));
            break;
        case "find-definition":
            result = findDefinition(args.project, args.file, parseInt(args.offset, 10));
            break;
        case "inspect-type":
            result = inspectType(args.project, args["type-name"], args.file);
            break;
        case "list-types":
            result = listTypes(args.project, args["name-filter"]);
            break;
        case "find-callers":
            result = findCallers(args.project, args.file, parseInt(args.offset, 10));
            break;
        case "find-implementations":
            result = findImplementations(args.project, args.file, parseInt(args.offset, 10));
            break;
        case "check":
            result = checkCompilation(args.project);
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
  check               Check compilation (type errors, syntax errors)

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
