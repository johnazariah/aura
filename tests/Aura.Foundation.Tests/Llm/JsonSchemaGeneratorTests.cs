// <copyright file="JsonSchemaGeneratorTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Llm;

using System.Text.Json;
using Aura.Foundation.Llm;
using Aura.Foundation.Llm.Schemas;
using FluentAssertions;
using Xunit;

/// <summary>
/// Tests for <see cref="JsonSchemaGenerator"/> and <see cref="WellKnownSchemas"/>.
/// </summary>
public class JsonSchemaGeneratorTests
{
    [Fact]
    public void Generate_ReActResponseDto_ProducesValidSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<ReActResponseDto>();

        // Assert - basic structure
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();

        // Check properties exist
        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("thought", out _).Should().BeTrue();
        properties.TryGetProperty("action", out _).Should().BeTrue();
        properties.TryGetProperty("action_input", out _).Should().BeTrue();

        // Check required fields
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        required.Should().Contain("thought");
        required.Should().Contain("action");
    }

    [Fact]
    public void Generate_WorkflowPlanDto_ProducesValidSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<WorkflowPlanDto>();

        // Assert - basic structure
        schema.GetProperty("type").GetString().Should().Be("object");

        // Check steps array
        var properties = schema.GetProperty("properties");
        var stepsProperty = properties.GetProperty("steps");
        stepsProperty.GetProperty("type").GetString().Should().Be("array");

        // Check items schema for steps
        var itemsSchema = stepsProperty.GetProperty("items");
        var stepProperties = itemsSchema.GetProperty("properties");
        stepProperties.TryGetProperty("name", out _).Should().BeTrue();
        stepProperties.TryGetProperty("capability", out _).Should().BeTrue();
        stepProperties.TryGetProperty("language", out _).Should().BeTrue();
        stepProperties.TryGetProperty("description", out _).Should().BeTrue();
    }

    [Fact]
    public void Generate_CodeModificationDto_ProducesValidSchema()
    {
        // Act
        var schema = JsonSchemaGenerator.Generate<CodeModificationDto>();

        // Assert - basic structure
        schema.GetProperty("type").GetString().Should().Be("object");

        var properties = schema.GetProperty("properties");
        properties.TryGetProperty("files", out _).Should().BeTrue();
        properties.TryGetProperty("explanation", out _).Should().BeTrue();

        // Check files array structure
        var filesProperty = properties.GetProperty("files");
        filesProperty.GetProperty("type").GetString().Should().Be("array");

        // Check file operation has enum for operation type
        var fileSchema = filesProperty.GetProperty("items");
        var fileProps = fileSchema.GetProperty("properties");
        var operationProp = fileProps.GetProperty("operation");
        operationProp.GetProperty("type").GetString().Should().Be("string");
        operationProp.TryGetProperty("enum", out var enumValues).Should().BeTrue();

        var enumStrings = enumValues.EnumerateArray().Select(e => e.GetString()).ToList();
        enumStrings.Should().Contain("create");
        enumStrings.Should().Contain("modify");
        enumStrings.Should().Contain("delete");
    }

    [Fact]
    public void WellKnownSchemas_ReActResponse_IsGenerated()
    {
        // Act
        var schema = WellKnownSchemas.ReActResponse;

        // Assert
        schema.Should().NotBeNull();
        schema.Name.Should().Be("react_response");
        schema.Strict.Should().BeTrue();
        schema.Schema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void WellKnownSchemas_WorkflowPlan_IsGenerated()
    {
        // Act
        var schema = WellKnownSchemas.WorkflowPlan;

        // Assert
        schema.Should().NotBeNull();
        schema.Name.Should().Be("workflow_plan");
        schema.Strict.Should().BeTrue();
        schema.Schema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void WellKnownSchemas_CodeModification_IsGenerated()
    {
        // Act
        var schema = WellKnownSchemas.CodeModification;

        // Assert
        schema.Should().NotBeNull();
        schema.Name.Should().Be("code_modification");
        schema.Strict.Should().BeTrue();
        schema.Schema.GetProperty("type").GetString().Should().Be("object");
    }

    [Fact]
    public void GeneratedSchemas_AreCompatibleWithJsonSerialization()
    {
        // Arrange - create sample DTOs
        var reactResponse = new ReActResponseDto
        {
            Thought = "I need to search for files",
            Action = "file_search",
            ActionInput = new { pattern = "*.cs" },
        };

        var workflowPlan = new WorkflowPlanDto
        {
            Steps =
            [
                new WorkflowStepDto
                {
                    Name = "Implement feature",
                    Capability = "coding",
                    Language = "csharp",
                    Description = "Create the implementation",
                },
            ],
        };

        var codeModification = new CodeModificationDto
        {
            Files =
            [
                new FileOperationDto
                {
                    Path = "src/Test.cs",
                    Operation = FileOperationType.Create,
                    Content = "// Test content",
                },
            ],
            Explanation = "Created test file",
        };

        // Act - serialize and deserialize
        var reactJson = JsonSerializer.Serialize(reactResponse);
        var workflowJson = JsonSerializer.Serialize(workflowPlan);
        var codeModJson = JsonSerializer.Serialize(codeModification);

        var deserializedReact = JsonSerializer.Deserialize<ReActResponseDto>(reactJson);
        var deserializedWorkflow = JsonSerializer.Deserialize<WorkflowPlanDto>(workflowJson);
        var deserializedCodeMod = JsonSerializer.Deserialize<CodeModificationDto>(codeModJson);

        // Assert
        deserializedReact.Should().NotBeNull();
        deserializedReact!.Thought.Should().Be("I need to search for files");
        deserializedReact.Action.Should().Be("file_search");

        deserializedWorkflow.Should().NotBeNull();
        deserializedWorkflow!.Steps.Should().HaveCount(1);
        deserializedWorkflow.Steps[0].Name.Should().Be("Implement feature");

        deserializedCodeMod.Should().NotBeNull();
        deserializedCodeMod!.Files.Should().HaveCount(1);
        deserializedCodeMod.Files[0].Operation.Should().Be(FileOperationType.Create);
    }

    [Fact]
    public void JsonPropertyName_IsRespected_ForSnakeCase()
    {
        // Arrange
        var dto = new ReActResponseDto
        {
            Thought = "test",
            Action = "finish",
            ActionInput = new { answer = "done" },
        };

        // Act
        var json = JsonSerializer.Serialize(dto);

        // Assert - should use snake_case for action_input
        json.Should().Contain("\"action_input\"");
        json.Should().NotContain("\"ActionInput\"");
    }
}
