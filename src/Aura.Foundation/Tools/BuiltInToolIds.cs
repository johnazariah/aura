// <copyright file="BuiltInToolIds.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools;

/// <summary>
/// Constants for built-in tool identifiers.
/// Use these instead of string literals when referencing tools.
/// </summary>
public static class BuiltInToolIds
{
    // File operations
    /// <summary>Read file contents.</summary>
    public const string FileRead = "file.read";

    /// <summary>Write file contents.</summary>
    public const string FileWrite = "file.write";

    /// <summary>Modify file with search/replace.</summary>
    public const string FileModify = "file.modify";

    /// <summary>List directory contents.</summary>
    public const string FileList = "file.list";

    /// <summary>Check if file exists.</summary>
    public const string FileExists = "file.exists";

    /// <summary>Delete a file.</summary>
    public const string FileDelete = "file.delete";

    // Search operations
    /// <summary>Search for text patterns in files.</summary>
    public const string SearchGrep = "search.grep";

    // Shell operations
    /// <summary>Execute shell commands.</summary>
    public const string ShellExecute = "shell.execute";

    // Git operations
    /// <summary>Get git status.</summary>
    public const string GitStatus = "git.status";

    /// <summary>Create a git commit.</summary>
    public const string GitCommit = "git.commit";

    /// <summary>Create or list git branches.</summary>
    public const string GitBranch = "git.branch";

    /// <summary>Checkout a git branch.</summary>
    public const string GitCheckout = "git.checkout";

    /// <summary>Push to remote repository.</summary>
    public const string GitPush = "git.push";

    /// <summary>Pull from remote repository.</summary>
    public const string GitPull = "git.pull";

    // Graph operations (for queries)
    /// <summary>Query the code graph.</summary>
    public const string GraphQuery = "graph.query";
}
