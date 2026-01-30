using System.Collections.Concurrent;
using System.Diagnostics;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.Extensions.Logging;

namespace Aura.Module.Developer.Services;

public sealed partial class RoslynRefactoringService
{
    /// <inheritdoc/>
    public async Task<RefactoringResult> MoveTypeToFileAsync(MoveTypeToFileRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Moving type {Type} to its own file in {Solution}", request.TypeName, request.SolutionPath);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            // Find the type
            var typeSymbol = await FindTypeAsync(solution, request.TypeName, ct);
            if (typeSymbol is null)
            {
                return RefactoringResult.Failed($"Type '{request.TypeName}' not found");
            }

            var typeDecl = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (typeDecl is null)
            {
                return RefactoringResult.Failed("Could not find type declaration");
            }

            var typeNode = await typeDecl.GetSyntaxAsync(ct) as BaseTypeDeclarationSyntax;
            if (typeNode is null)
            {
                return RefactoringResult.Failed("Could not parse type declaration");
            }

            var sourceDoc = solution.GetDocument(typeDecl.SyntaxTree);
            if (sourceDoc?.FilePath is null)
            {
                return RefactoringResult.Failed("Could not find source document");
            }

            var sourceRoot = await sourceDoc.GetSyntaxRootAsync(ct);
            if (sourceRoot is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Determine target file path
            var targetDir = request.TargetDirectory ?? Path.GetDirectoryName(sourceDoc.FilePath)!;
            var targetFileName = request.TargetFileName ?? $"{request.TypeName}.cs";
            var targetPath = Path.Combine(targetDir, targetFileName);
            // Check if already in correct file
            var sourceFileName = Path.GetFileNameWithoutExtension(sourceDoc.FilePath);
            if (sourceFileName.Equals(request.TypeName, StringComparison.OrdinalIgnoreCase))
            {
                return RefactoringResult.Succeeded($"Type '{request.TypeName}' is already in file '{sourceFileName}.cs'", []);
            }

            // Check if target file already exists
            if (File.Exists(targetPath))
            {
                return RefactoringResult.Failed($"Target file already exists: {targetPath}");
            }

            // Count types in source file (classes, interfaces, records, structs, enums)
            var typesInFile = sourceRoot.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax).ToList();
            var isOnlyTypeInFile = typesInFile.Count == 1;
            if (isOnlyTypeInFile && request.UseGitMove)
            {
                // Simple case: just rename the file using git mv
                return await MoveFileWithGitAsync(sourceDoc.FilePath, targetPath, request.Preview, ct);
            }

            // Complex case: extract type to new file
            return await ExtractTypeToNewFileAsync(solution, sourceDoc, sourceRoot, typeNode, targetPath, request.Preview, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move type to file");
            return RefactoringResult.Failed($"Failed to move type: {ex.Message}");
        }
    }

    private async Task<RefactoringResult> MoveFileWithGitAsync(string sourcePath, string targetPath, bool preview, CancellationToken ct)
    {
        _logger.LogDebug("Moving file with git: {Source} -> {Target}", sourcePath, targetPath);
        if (preview)
        {
            return new RefactoringResult
            {
                Success = true,
                Message = $"Preview: Would rename file using 'git mv {Path.GetFileName(sourcePath)} {Path.GetFileName(targetPath)}'",
                ModifiedFiles = [sourcePath],
                CreatedFiles = [targetPath]
            };
        }

        // Use git mv to preserve history
        var workingDir = Path.GetDirectoryName(sourcePath)!;
        var sourceFileName = Path.GetFileName(sourcePath);
        var targetFileName = Path.GetFileName(targetPath);
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"mv \"{sourceFileName}\" \"{targetFileName}\"",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi);
        if (process is null)
        {
            return RefactoringResult.Failed("Failed to start git process");
        }

        await process.WaitForExitAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        if (process.ExitCode != 0)
        {
            _logger.LogWarning("git mv failed: {Error}", error);
            // Fall back to regular file move
            File.Move(sourcePath, targetPath);
            _logger.LogInformation("Fell back to File.Move");
        }

        _workspaceService.ClearCache();
        return new RefactoringResult
        {
            Success = true,
            Message = $"Renamed file '{Path.GetFileName(sourcePath)}' to '{Path.GetFileName(targetPath)}'",
            ModifiedFiles = [],
            CreatedFiles = [targetPath],
            DeletedFiles = [sourcePath]
        };
    }

    private async Task<RefactoringResult> ExtractTypeToNewFileAsync(Solution solution, Document sourceDoc, SyntaxNode sourceRoot, BaseTypeDeclarationSyntax typeNode, string targetPath, bool preview, CancellationToken ct)
    {
        _logger.LogDebug("Extracting type to new file: {Target}", targetPath);
        // Get namespace
        var namespaceDecl = typeNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var namespaceName = namespaceDecl switch
        {
            NamespaceDeclarationSyntax ns => ns.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
            _ => "Unknown"
        };
        // Collect using directives from source
        var usings = sourceRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        // Build new file content
        var newFileRoot = SyntaxFactory.CompilationUnit().WithUsings(SyntaxFactory.List(usings)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeNode))));
        // Remove type from source file
        var newSourceRoot = sourceRoot.RemoveNode(typeNode, SyntaxRemoveOptions.KeepNoTrivia);
        if (newSourceRoot is null)
        {
            return RefactoringResult.Failed("Failed to remove type from source file");
        }

        var formattedNewFile = Formatter.Format(newFileRoot, solution.Workspace);
        var formattedSource = Formatter.Format(newSourceRoot, solution.Workspace);
        if (preview)
        {
            var originalText = (await sourceDoc.GetTextAsync(ct)).ToString();
            return new RefactoringResult
            {
                Success = true,
                Message = $"Preview: Would extract type to new file and update source",
                Preview = [new FileChange(sourceDoc.FilePath!, originalText, formattedSource.ToFullString()), new FileChange(targetPath, "", formattedNewFile.ToFullString())],
                CreatedFiles = [targetPath],
                ModifiedFiles = [sourceDoc.FilePath!]
            };
        }

        // Write new file
        using (await AcquireFileLockAsync(targetPath, ct))
        {
            await File.WriteAllTextAsync(targetPath, formattedNewFile.ToFullString(), ct);
        }

        // Update source file
        using (await AcquireFileLockAsync(sourceDoc.FilePath!, ct))
        {
            await File.WriteAllTextAsync(sourceDoc.FilePath!, formattedSource.ToFullString(), ct);
        }

        _workspaceService.ClearCache();
        return new RefactoringResult
        {
            Success = true,
            Message = $"Extracted type '{typeNode.Identifier.Text}' to '{Path.GetFileName(targetPath)}'",
            CreatedFiles = [targetPath],
            ModifiedFiles = [sourceDoc.FilePath!]
        };
    }

    /// <inheritdoc/>
    public async Task<RefactoringResult> MoveMembersToPartialAsync(MoveMembersToPartialRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Moving {Count} members from {Class} to partial file {Target}", request.MemberNames.Count, request.ClassName, request.TargetFileName);
        if (!File.Exists(request.SolutionPath))
        {
            return RefactoringResult.Failed($"Solution file not found: {request.SolutionPath}");
        }

        if (request.MemberNames.Count == 0)
        {
            return RefactoringResult.Failed("No member names specified");
        }

        try
        {
            var solution = await _workspaceService.GetSolutionAsync(request.SolutionPath, ct);
            // Find the class
            var typeSymbol = await FindTypeAsync(solution, request.ClassName, ct);
            if (typeSymbol is null)
            {
                return RefactoringResult.Failed($"Class '{request.ClassName}' not found");
            }

            // Get the syntax reference
            var typeRef = typeSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (typeRef is null)
            {
                return RefactoringResult.Failed("Could not find class declaration");
            }

            var typeSyntax = await typeRef.GetSyntaxAsync(ct) as TypeDeclarationSyntax;
            if (typeSyntax is null)
            {
                return RefactoringResult.Failed("Could not parse class declaration");
            }

            var sourceDoc = solution.GetDocument(typeRef.SyntaxTree);
            if (sourceDoc?.FilePath is null)
            {
                return RefactoringResult.Failed("Could not find source document");
            }

            var sourceRoot = await sourceDoc.GetSyntaxRootAsync(ct);
            if (sourceRoot is null)
            {
                return RefactoringResult.Failed("Could not get syntax root");
            }

            // Determine target path
            var targetDir = request.TargetDirectory ?? Path.GetDirectoryName(sourceDoc.FilePath)!;
            var targetPath = Path.Combine(targetDir, request.TargetFileName);
            if (File.Exists(targetPath))
            {
                return RefactoringResult.Failed($"Target file already exists: {targetPath}");
            }

            // Find members to move
            var memberNamesSet = new HashSet<string>(request.MemberNames, StringComparer.Ordinal);
            var membersToMove = new List<MemberDeclarationSyntax>();
            var membersToKeep = new List<MemberDeclarationSyntax>();
            foreach (var member in typeSyntax.Members)
            {
                var memberName = GetMemberName(member);
                if (memberName != null && memberNamesSet.Contains(memberName))
                {
                    membersToMove.Add(member);
                    memberNamesSet.Remove(memberName);
                }
                else
                {
                    membersToKeep.Add(member);
                }
            }

            if (memberNamesSet.Count > 0)
            {
                return RefactoringResult.Failed($"Members not found: {string.Join(", ", memberNamesSet)}");
            }

            if (membersToMove.Count == 0)
            {
                return RefactoringResult.Failed("No members matched the specified names");
            }

            // Get namespace and usings
            var namespaceDecl = typeSyntax.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDecl switch
            {
                NamespaceDeclarationSyntax ns => ns.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax fsns => fsns.Name.ToString(),
                _ => typeSymbol.ContainingNamespace?.ToString() ?? "Unknown"
            };
            var usings = sourceRoot.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
            // Ensure source class has partial modifier
            var sourceModified = false;
            var newSourceRoot = sourceRoot;
            if (!typeSyntax.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                if (request.EnsureSourceIsPartial)
                {
                    // Add partial modifier
                    var partialToken = SyntaxFactory.Token(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
                    var newModifiers = typeSyntax.Modifiers.Add(partialToken);
                    var newTypeSyntax = typeSyntax.WithModifiers(newModifiers);
                    newSourceRoot = sourceRoot.ReplaceNode(typeSyntax, newTypeSyntax);
                    typeSyntax = (TypeDeclarationSyntax)newSourceRoot.DescendantNodes().First(n => n is TypeDeclarationSyntax t && t.Identifier.Text == request.ClassName);
                    sourceModified = true;
                }
                else
                {
                    return RefactoringResult.Failed($"Class '{request.ClassName}' is not partial. Set EnsureSourceIsPartial=true to add the modifier.");
                }
            }

            // Remove moved members from source
            var nodesToRemove = typeSyntax.Members.Where(m => membersToMove.Any(mtm => mtm.IsEquivalentTo(m))).ToList();
            var modifiedTypeSyntax = typeSyntax.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia);
            if (modifiedTypeSyntax is null)
            {
                return RefactoringResult.Failed("Failed to remove members from source class");
            }

            newSourceRoot = newSourceRoot.ReplaceNode(typeSyntax, modifiedTypeSyntax);
            // Build target partial class
            var targetTypeModifiers = SyntaxFactory.TokenList(typeSyntax.Modifiers.Where(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.InternalKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PrivateKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ProtectedKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SealedKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.AbstractKeyword) || m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)));
            // Ensure partial is included
            if (!targetTypeModifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)))
            {
                targetTypeModifiers = targetTypeModifiers.Add(SyntaxFactory.Token(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            }

            var targetTypeSyntax = SyntaxFactory.ClassDeclaration(typeSyntax.Identifier).WithModifiers(targetTypeModifiers).WithTypeParameterList(typeSyntax.TypeParameterList).WithConstraintClauses(typeSyntax.ConstraintClauses).WithMembers(SyntaxFactory.List(membersToMove)).WithLeadingTrivia(SyntaxFactory.LineFeed);
            // Build new file
            var targetFileRoot = SyntaxFactory.CompilationUnit().WithUsings(SyntaxFactory.List(usings)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(SyntaxFactory.FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(namespaceName)).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(targetTypeSyntax)))).NormalizeWhitespace();
            var formattedTarget = Formatter.Format(targetFileRoot, solution.Workspace);
            var formattedSource = Formatter.Format(newSourceRoot, solution.Workspace);
            // Normalize to LF line endings
            var targetText = formattedTarget.ToFullString().Replace("\r\n", "\n").Replace("\r", "\n");
            var sourceText = formattedSource.ToFullString().Replace("\r\n", "\n").Replace("\r", "\n");
            // Ensure trailing newline
            if (!targetText.EndsWith('\n'))
                targetText += '\n';
            if (!sourceText.EndsWith('\n'))
                sourceText += '\n';
            if (request.Preview)
            {
                var originalText = (await sourceDoc.GetTextAsync(ct)).ToString();
                return new RefactoringResult
                {
                    Success = true,
                    Message = $"Preview: Would move {membersToMove.Count} member(s) to partial file",
                    Preview = [new FileChange(sourceDoc.FilePath!, originalText, sourceText), new FileChange(targetPath, "", targetText)],
                    CreatedFiles = [targetPath],
                    ModifiedFiles = [sourceDoc.FilePath!]
                };
            }

            // Write files
            using (await AcquireFileLockAsync(targetPath, ct))
            {
                await File.WriteAllTextAsync(targetPath, targetText, ct);
            }

            using (await AcquireFileLockAsync(sourceDoc.FilePath!, ct))
            {
                await File.WriteAllTextAsync(sourceDoc.FilePath!, sourceText, ct);
            }

            _workspaceService.ClearCache();
            var partialNote = sourceModified ? " (added partial modifier)" : "";
            return new RefactoringResult
            {
                Success = true,
                Message = $"Moved {membersToMove.Count} member(s) to '{request.TargetFileName}'{partialNote}",
                CreatedFiles = [targetPath],
                ModifiedFiles = [sourceDoc.FilePath!]
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move members to partial file");
            return RefactoringResult.Failed($"Failed to move members: {ex.Message}");
        }
    }
}
