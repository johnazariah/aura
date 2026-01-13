// <copyright file="JsonRpcTypes.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Api.Mcp;

using System.Text.Json.Serialization;

/// <summary>
/// JSON-RPC 2.0 request message.
/// </summary>
public sealed class JsonRpcRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC version (always "2.0").
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    /// <summary>
    /// Gets or sets the method name.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request ID.
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// Gets or sets the method parameters.
    /// </summary>
    [JsonPropertyName("params")]
    public System.Text.Json.JsonElement? Params { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 response message.
/// </summary>
public sealed class JsonRpcResponse
{
    /// <summary>
    /// Gets the JSON-RPC version (always "2.0").
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc => "2.0";

    /// <summary>
    /// Gets or sets the request ID.
    /// </summary>
    [JsonPropertyName("id")]
    public object? Id { get; set; }

    /// <summary>
    /// Gets or sets the result (mutually exclusive with Error).
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// Gets or sets the error (mutually exclusive with Result).
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
}

/// <summary>
/// JSON-RPC 2.0 error object.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional error data.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

/// <summary>
/// MCP tool definition for tools/list response.
/// </summary>
public sealed class McpToolDefinition
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input schema (JSON Schema).
    /// </summary>
    [JsonPropertyName("inputSchema")]
    public object InputSchema { get; set; } = new { };
}

/// <summary>
/// MCP content block for tool results.
/// </summary>
public sealed class McpContent
{
    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    /// <summary>
    /// Gets or sets the text content.
    /// </summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
