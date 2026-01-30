// <copyright file="JsonSchemaGenerator.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Llm.Schemas;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Generates JSON schemas from .NET types using reflection.
/// Schemas are compatible with OpenAI's structured output feature.
/// </summary>
/// <remarks>
/// This generator creates schemas that match OpenAI's strict mode requirements:
/// - All properties are required unless nullable
/// - additionalProperties is set to false
/// - Enum values are serialized as strings
/// </remarks>
public static class JsonSchemaGenerator
{
    private static readonly ConcurrentDictionary<Type, JsonElement> SchemaCache = new();

    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <returns>A JsonElement containing the schema.</returns>
    public static JsonElement Generate<T>() => Generate(typeof(T));

    /// <summary>
    /// Generates a JSON schema for the specified type.
    /// </summary>
    /// <param name="type">The type to generate a schema for.</param>
    /// <returns>A JsonElement containing the schema.</returns>
    public static JsonElement Generate(Type type)
    {
        return SchemaCache.GetOrAdd(type, GenerateSchema);
    }

    /// <summary>
    /// Creates a <see cref="JsonSchema"/> record with the generated schema.
    /// </summary>
    /// <typeparam name="T">The type to generate a schema for.</typeparam>
    /// <param name="name">The name for the schema.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="strict">Whether to enforce strict schema adherence.</param>
    /// <returns>A JsonSchema record.</returns>
    public static JsonSchema CreateSchema<T>(string name, string? description = null, bool strict = true)
    {
        return new JsonSchema(
            Name: name,
            Schema: Generate<T>(),
            Description: description,
            Strict: strict);
    }

    private static JsonElement GenerateSchema(Type type)
    {
        var schema = GenerateTypeSchema(type, new HashSet<Type>());
        var json = JsonSerializer.Serialize(schema);
        return JsonDocument.Parse(json).RootElement;
    }

    private static Dictionary<string, object> GenerateTypeSchema(Type type, HashSet<Type> visitedTypes)
    {
        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            var innerSchema = GenerateTypeSchema(underlyingType, visitedTypes);
            // For nullable, we could add "null" to the type, but OpenAI strict mode doesn't support it well
            return innerSchema;
        }

        // Handle primitive types
        if (type == typeof(string))
        {
            return new Dictionary<string, object> { ["type"] = "string" };
        }

        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
        {
            return new Dictionary<string, object> { ["type"] = "integer" };
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return new Dictionary<string, object> { ["type"] = "number" };
        }

        if (type == typeof(bool))
        {
            return new Dictionary<string, object> { ["type"] = "boolean" };
        }

        // Handle enums
        if (type.IsEnum)
        {
            var enumValues = new List<string>();
            foreach (var value in Enum.GetValues(type))
            {
                var memberInfo = type.GetMember(value.ToString()!).FirstOrDefault();
                var jsonAttr = memberInfo?.GetCustomAttribute<JsonPropertyNameAttribute>();
                enumValues.Add(jsonAttr?.Name ?? value.ToString()!.ToLowerInvariant());
            }

            return new Dictionary<string, object>
            {
                ["type"] = "string",
                ["enum"] = enumValues,
            };
        }

        // Handle arrays and lists
        if (type.IsArray)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = GenerateTypeSchema(type.GetElementType()!, visitedTypes),
            };
        }

        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            if (genericDef == typeof(IReadOnlyList<>) ||
                genericDef == typeof(IList<>) ||
                genericDef == typeof(List<>) ||
                genericDef == typeof(IEnumerable<>) ||
                genericDef == typeof(ICollection<>))
            {
                return new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = GenerateTypeSchema(type.GetGenericArguments()[0], visitedTypes),
                };
            }
        }

        // Handle object (any JSON value)
        if (type == typeof(object))
        {
            return new Dictionary<string, object> { ["type"] = "object" };
        }

        // Handle complex types (records, classes)
        if (!visitedTypes.Add(type))
        {
            // Circular reference - return object type to break the cycle
            return new Dictionary<string, object> { ["type"] = "object" };
        }

        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // Skip properties with JsonIgnore
            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
            {
                continue;
            }

            // Get the JSON property name
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(prop.Name);

            // Generate the property schema
            var propSchema = GenerateTypeSchema(prop.PropertyType, new HashSet<Type>(visitedTypes));

            // Add description from XML docs if available (would need additional tooling)
            // For now, we'll skip descriptions

            properties[jsonName] = propSchema;

            // Determine if required
            var isNullable = IsNullableProperty(prop);
            var hasRequiredAttr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null
                || prop.Name.StartsWith("required ", StringComparison.OrdinalIgnoreCase);

            // Check for 'required' keyword in C# 11+
            var isRequiredMember = prop.GetCustomAttributes()
                .Any(a => a.GetType().Name == "RequiredMemberAttribute");

            if (!isNullable || hasRequiredAttr || isRequiredMember)
            {
                required.Add(jsonName);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static bool IsNullableProperty(PropertyInfo prop)
    {
        // Check for Nullable<T>
        if (Nullable.GetUnderlyingType(prop.PropertyType) != null)
        {
            return true;
        }

        // Check for nullable reference type using NullabilityInfoContext
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable;
    }
}
