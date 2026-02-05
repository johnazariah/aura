// <copyright file="CSharpOperationDto.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Agents;

using System.Text.Json.Serialization;

/// <summary>
/// DTO for C# coding operations extracted by LLM.
/// Used by RoslynCodingAgent to execute deterministic Roslyn operations.
/// </summary>
public sealed record CSharpOperationsDto
{
    /// <summary>
    /// List of operations to execute in order.
    /// </summary>
    [JsonPropertyName("operations")]
    public required IReadOnlyList<CSharpOperationDto> Operations { get; init; }

    /// <summary>
    /// Brief summary of what these operations accomplish.
    /// </summary>
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
}

/// <summary>
/// A single C# code operation to execute.
/// </summary>
public sealed record CSharpOperationDto
{
    /// <summary>
    /// The operation type: add_method, add_property, add_field, create_type,
    /// implement_interface, generate_constructor, generate_tests, rename, read_file.
    /// </summary>
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }

    /// <summary>
    /// The target class name (e.g., "Calculator", "UserService").
    /// Required for: add_method, add_property, add_field, implement_interface, generate_constructor, generate_tests.
    /// </summary>
    [JsonPropertyName("className")]
    public string? ClassName { get; init; }

    /// <summary>
    /// The file path to read or create. Required for: read_file, create_type.
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    /// <summary>
    /// Method name for add_method operation.
    /// </summary>
    [JsonPropertyName("methodName")]
    public string? MethodName { get; init; }

    /// <summary>
    /// Return type for add_method operation (e.g., "int", "void", "Task&lt;string&gt;").
    /// </summary>
    [JsonPropertyName("returnType")]
    public string? ReturnType { get; init; }

    /// <summary>
    /// Parameters for add_method as a string (e.g., "int[] numbers", "string name, int age").
    /// </summary>
    [JsonPropertyName("parameters")]
    public string? Parameters { get; init; }

    /// <summary>
    /// Method or property body code.
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>
    /// Property name for add_property operation.
    /// </summary>
    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; init; }

    /// <summary>
    /// Property type for add_property operation (e.g., "string", "int?").
    /// </summary>
    [JsonPropertyName("propertyType")]
    public string? PropertyType { get; init; }

    /// <summary>
    /// Initial value for add_property operation (e.g., "string.Empty", "0", "new List&lt;int&gt;()").
    /// </summary>
    [JsonPropertyName("initialValue")]
    public string? InitialValue { get; init; }

    /// <summary>
    /// Whether this is a field instead of a property (for add_property with isField=true).
    /// </summary>
    [JsonPropertyName("isField")]
    public bool IsField { get; init; }

    /// <summary>
    /// Whether the property uses init-only setter (C# 9+).
    /// </summary>
    [JsonPropertyName("hasInit")]
    public bool HasInit { get; init; }

    /// <summary>
    /// Whether the property has the required modifier (C# 11+).
    /// </summary>
    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; init; }

    /// <summary>
    /// Whether the field/property is readonly.
    /// </summary>
    [JsonPropertyName("isReadonly")]
    public bool IsReadonly { get; init; }

    /// <summary>
    /// Access modifier: public, private, internal, protected. Default is public.
    /// </summary>
    [JsonPropertyName("accessModifier")]
    public string? AccessModifier { get; init; }

    /// <summary>
    /// XML documentation for the member.
    /// </summary>
    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }

    /// <summary>
    /// Interface name for implement_interface operation.
    /// </summary>
    [JsonPropertyName("interfaceName")]
    public string? InterfaceName { get; init; }

    /// <summary>
    /// Type kind for create_type: class, interface, record, struct, enum.
    /// </summary>
    [JsonPropertyName("typeKind")]
    public string? TypeKind { get; init; }

    /// <summary>
    /// Whether the type or method is static.
    /// </summary>
    [JsonPropertyName("isStatic")]
    public bool IsStatic { get; init; }

    /// <summary>
    /// Whether the method is async. If not specified, auto-detected from return type and body.
    /// </summary>
    [JsonPropertyName("isAsync")]
    public bool? IsAsync { get; init; }

    /// <summary>
    /// Method modifier: virtual, override, abstract, sealed, or new.
    /// </summary>
    [JsonPropertyName("methodModifier")]
    public string? MethodModifier { get; init; }

    /// <summary>
    /// Whether to generate as extension method (first parameter gets 'this' modifier).
    /// </summary>
    [JsonPropertyName("isExtension")]
    public bool IsExtension { get; init; }

    /// <summary>
    /// Generic type parameters for the method (e.g., "T", "TEntity where TEntity : class").
    /// </summary>
    [JsonPropertyName("typeParameters")]
    public IReadOnlyList<TypeParameterDto>? TypeParameters { get; init; }

    /// <summary>
    /// Namespace for create_type operation.
    /// </summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; init; }

    /// <summary>
    /// New name for rename operation.
    /// </summary>
    [JsonPropertyName("newName")]
    public string? NewName { get; init; }

    /// <summary>
    /// Old/current name for rename operation.
    /// </summary>
    [JsonPropertyName("oldName")]
    public string? OldName { get; init; }

    /// <summary>
    /// Member names to include in constructor for generate_constructor.
    /// </summary>
    [JsonPropertyName("members")]
    public IReadOnlyList<string>? Members { get; init; }

    /// <summary>
    /// Additional using directives for create_type operation.
    /// </summary>
    [JsonPropertyName("additionalUsings")]
    public IReadOnlyList<string>? AdditionalUsings { get; init; }

    /// <summary>
    /// Base class for create_type operation.
    /// </summary>
    [JsonPropertyName("baseClass")]
    public string? BaseClass { get; init; }

    /// <summary>
    /// Interfaces to implement for create_type operation.
    /// </summary>
    [JsonPropertyName("interfaces")]
    public IReadOnlyList<string>? Interfaces { get; init; }

    /// <summary>
    /// Enum member names for create_type with typeKind=enum.
    /// </summary>
    [JsonPropertyName("enumMembers")]
    public IReadOnlyList<string>? EnumMembers { get; init; }

    /// <summary>
    /// Parameters to add for change_signature operation.
    /// Each object should have "name", "type", and optionally "defaultValue".
    /// </summary>
    [JsonPropertyName("addParameters")]
    public IReadOnlyList<ParameterDto>? AddParameters { get; init; }

    /// <summary>
    /// Parameter names to remove for change_signature operation.
    /// </summary>
    [JsonPropertyName("removeParameters")]
    public IReadOnlyList<string>? RemoveParameters { get; init; }

    /// <summary>
    /// Attributes to apply to the member (method, property, or type).
    /// Example: [["HttpGet"], ["HttpGet", "{id}"]]
    /// </summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<AttributeDto>? Attributes { get; init; }

    /// <summary>
    /// Explanation of why this operation is needed.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Attribute specification for add_method, add_property, or create_type operations.
/// </summary>
public sealed record AttributeDto
{
    /// <summary>Attribute name without brackets (e.g., "HttpGet", "ApiController").</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Attribute arguments (e.g., ["{id}"] for HttpGet("{id}")).</summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyList<string>? Arguments { get; init; }
}

/// <summary>
/// Parameter specification for change_signature operation.
/// </summary>
public sealed record ParameterDto
{
    /// <summary>Parameter name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Parameter type.</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>Optional default value.</summary>
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }
}

/// <summary>
/// Type parameter specification for generic methods/types.
/// </summary>
public sealed record TypeParameterDto
{
    /// <summary>Type parameter name (e.g., "T", "TEntity").</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Constraints (e.g., ["class"], ["IEntity", "new()"]).</summary>
    [JsonPropertyName("constraints")]
    public IReadOnlyList<string>? Constraints { get; init; }
}
