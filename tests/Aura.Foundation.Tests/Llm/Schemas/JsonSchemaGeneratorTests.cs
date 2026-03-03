// <copyright file="JsonSchemaGeneratorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Llm.Schemas;

using System.Text.Json;
using System.Text.Json.Serialization;
using Aura.Foundation.Llm.Schemas;
using FluentAssertions;
using Xunit;

public class JsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_ForString_ReturnsStringSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<string>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Generate_ForInt_ReturnsIntegerSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<int>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("integer");
    }

    [Fact]
    public void Generate_ForBool_ReturnsBooleanSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<bool>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("boolean");
    }

    [Fact]
    public void Generate_ForDouble_ReturnsNumberSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<double>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("number");
    }

    [Fact]
    public void Generate_ForRecordWithProperties_ReturnsObjectSchemaWithProperties()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleRecord>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("properties").GetProperty("name").GetProperty("type").GetString().Should().Be("string");
        schema.GetProperty("properties").GetProperty("age").GetProperty("type").GetString().Should().Be("integer");
        schema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Generate_ForRecordWithProperties_MarksMembersAsRequired()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleRecord>();

        // Assert
        schema.TryGetProperty("required", out var required).Should().BeTrue();
        var requiredList = new List<string>();
        foreach (var item in required.EnumerateArray())
        {
            requiredList.Add(item.GetString()!);
        }

        requiredList.Should().Contain("name");
        requiredList.Should().Contain("age");
    }

    [Fact]
    public void Generate_ForTypeWithNullableProperty_DoesNotRequireNullableField()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TypeWithNullable>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("properties").GetProperty("label").GetProperty("type").GetString().Should().Be("string");

        if (schema.TryGetProperty("required", out var required))
        {
            var requiredList = new List<string>();
            foreach (var item in required.EnumerateArray())
            {
                requiredList.Add(item.GetString()!);
            }

            requiredList.Should().Contain("label");
            requiredList.Should().NotContain("description");
        }
    }

    [Fact]
    public void Generate_ForEnum_ReturnsStringSchemaWithEnumValues()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TestColor>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("string");
        schema.TryGetProperty("enum", out var enumValues).Should().BeTrue();

        var values = new List<string>();
        foreach (var item in enumValues.EnumerateArray())
        {
            values.Add(item.GetString()!);
        }

        values.Should().Contain("red");
        values.Should().Contain("green");
        values.Should().Contain("blue");
    }

    [Fact]
    public void Generate_ForListOfStrings_ReturnsArraySchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<List<string>>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("items").GetProperty("type").GetString().Should().Be("string");
    }

    [Fact]
    public void Generate_ForArray_ReturnsArraySchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<int[]>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("items").GetProperty("type").GetString().Should().Be("integer");
    }

    [Fact]
    public void Generate_UsesCamelCasePropertyNames()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<SimpleRecord>();

        // Assert
        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("name", out _).Should().BeTrue();
        properties.TryGetProperty("age", out _).Should().BeTrue();
    }

    [Fact]
    public void Generate_RespectsJsonPropertyNameAttribute()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TypeWithJsonAttribute>();

        // Assert
        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("custom_name", out _).Should().BeTrue();
    }

    [Fact]
    public void Generate_SkipsJsonIgnoredProperties()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<TypeWithJsonIgnore>();

        // Assert
        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("visible", out _).Should().BeTrue();
        properties.TryGetProperty("hidden", out _).Should().BeFalse();
    }

    [Fact]
    public void CreateSchema_ReturnsJsonSchemaRecord()
    {
        // Act
        var schema = JsonSchemaGenerator.CreateSchema<SimpleRecord>("test-schema", "A test schema");

        // Assert
        schema.Name.Should().Be("test-schema");
        schema.Description.Should().Be("A test schema");
        schema.Strict.Should().BeTrue();
        schema.Schema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void Generate_ForNestedType_ReturnsNestedObjectSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<OuterType>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("object");
        var innerProp = schema.GetProperty("properties").GetProperty("inner");
        innerProp.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void Generate_ForNullableValueType_ReturnsUnderlyingTypeSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<int?>();

        // Assert
        schema.GetProperty("type").GetString().Should().Be("integer");
    }

    // Test types
    public record SimpleRecord(string Name, int Age);

    public record TypeWithNullable
    {
        public string Label { get; init; } = "";
        public string? Description { get; init; }
    }

    public enum TestColor
    {
        Red,
        Green,
        Blue,
    }

    public record TypeWithJsonAttribute
    {
        [JsonPropertyName("custom_name")]
        public string CustomName { get; init; } = "";
    }

    public record TypeWithJsonIgnore
    {
        public string Visible { get; init; } = "";

        [JsonIgnore]
        public string Hidden { get; init; } = "";
    }

    public record OuterType
    {
        public InnerType Inner { get; init; } = new();
    }

    public record InnerType
    {
        public string Value { get; init; } = "";
    }
}
