// <copyright file="EchoTool.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tools.BuiltIn;

/// <summary>
/// Input for the echo tool.
/// </summary>
public record EchoInput
{
    /// <summary>The message to echo back.</summary>
    public required string Message { get; init; }

    /// <summary>Optional prefix to add.</summary>
    public string? Prefix { get; init; }
}

/// <summary>
/// Output from the echo tool.
/// </summary>
public record EchoOutput
{
    /// <summary>The echoed message.</summary>
    public required string EchoedMessage { get; init; }

    /// <summary>The length of the original message.</summary>
    public int OriginalLength { get; init; }
}

/// <summary>
/// A simple echo tool for testing the ReAct framework.
/// Echoes back the input message with optional prefix.
/// </summary>
public class EchoTool : TypedToolBase<EchoInput, EchoOutput>
{
    /// <inheritdoc/>
    public override string ToolId => "echo";

    /// <inheritdoc/>
    public override string Name => "Echo";

    /// <inheritdoc/>
    public override string Description => "Echoes back the input message. Use for testing.";

    /// <inheritdoc/>
    public override bool RequiresConfirmation => false;

    /// <inheritdoc/>
    public override Task<ToolResult<EchoOutput>> ExecuteAsync(
        EchoInput input,
        CancellationToken ct = default)
    {
        var echoed = input.Prefix is not null
            ? $"{input.Prefix}: {input.Message}"
            : input.Message;

        var output = new EchoOutput
        {
            EchoedMessage = echoed,
            OriginalLength = input.Message.Length,
        };

        return Task.FromResult(ToolResult<EchoOutput>.Ok(output));
    }
}
