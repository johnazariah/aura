using System.Text.Json;
using Aura.Api.Mcp.Tools;
using Aura.Api.Services;
using Aura.Foundation.Data.Entities;
using Aura.Foundation.Git;
using Aura.Foundation.Rag;
using Aura.Module.Developer.Data.Entities;
using Aura.Module.Developer.GitHub;
using Aura.Module.Developer.Services;
using Aura.Module.Developer.Services.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Extensions.Logging;
using RefactoringParameterInfo = Aura.Module.Developer.Services.ParameterInfo;

namespace Aura.Api.Mcp;

public sealed partial class McpHandler
{
    /// <summary>
    /// aura_generate - Create new code elements.
    /// Routes to: implement_interface, constructor, property, method, tests.
    /// </summary>
    private async Task<object> GenerateAsync(JsonElement? args, CancellationToken ct)
    {
        var operation = args.GetRequiredString("operation");
        return operation switch
        {
            "implement_interface" => await ImplementInterfaceAsync(args, ct),
            "constructor" => await GenerateConstructorAsync(args, ct),
            "property" => await AddPropertyAsync(args, ct),
            "method" => await AddMethodAsync(args, ct),
            "create_type" => await CreateTypeAsync(args, ct),
            "tests" => await GenerateTestsAsync(args, ct),
            _ => throw new ArgumentException($"Unknown generate operation: {operation}")
        };
    }

    private async Task<object> ImplementInterfaceAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args.GetStringOrDefault("className");
        var interfaceName = args.GetStringOrDefault("interfaceName");
        var solutionPath = args.GetStringOrDefault("solutionPath");
        var explicitImpl = false;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("explicitImplementation", out var expEl))
                explicitImpl = expEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ImplementInterfaceAsync(new ImplementInterfaceRequest { ClassName = className, InterfaceName = interfaceName, SolutionPath = solutionPath, ExplicitImplementation = explicitImpl, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> GenerateConstructorAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args.GetStringOrDefault("className");
        var solutionPath = args.GetStringOrDefault("solutionPath");
        List<string>? members = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("members", out var memEl) && memEl.ValueKind == JsonValueKind.Array)
            {
                members = memEl.EnumerateArray().Select(m => m.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.GenerateConstructorAsync(new GenerateConstructorRequest { ClassName = className, SolutionPath = solutionPath, Members = members, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> ExtractInterfaceAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args.GetStringOrDefault("className");
        var interfaceName = args.GetStringOrDefault("interfaceName");
        var solutionPath = args.GetStringOrDefault("solutionPath");
        List<string>? members = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("members", out var memEl) && memEl.ValueKind == JsonValueKind.Array)
            {
                members = memEl.EnumerateArray().Select(m => m.GetString() ?? "").ToList();
            }

            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        var result = await _refactoringService.ExtractInterfaceAsync(new ExtractInterfaceRequest { ClassName = className, InterfaceName = interfaceName, SolutionPath = solutionPath, Members = members, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            createdFiles = result.CreatedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> AddPropertyAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args.GetStringOrDefault("className");
        var propertyName = args.GetStringOrDefault("propertyName");
        var propertyType = args.GetStringOrDefault("propertyType");
        var solutionPath = args.GetStringOrDefault("solutionPath");
        var accessModifier = "public";
        var hasGetter = true;
        var hasSetter = true;
        var hasInit = false;
        var isRequired = false;
        string? initialValue = null;
        var isField = false;
        var isReadonly = false;
        var isStatic = false;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("hasGetter", out var gEl))
                hasGetter = gEl.GetBoolean();
            if (args.Value.TryGetProperty("hasSetter", out var sEl))
                hasSetter = sEl.GetBoolean();
            if (args.Value.TryGetProperty("hasInit", out var hiEl))
                hasInit = hiEl.GetBoolean();
            if (args.Value.TryGetProperty("isRequired", out var reqEl))
                isRequired = reqEl.GetBoolean();
            if (args.Value.TryGetProperty("initialValue", out var ivEl))
                initialValue = ivEl.GetString();
            if (args.Value.TryGetProperty("isField", out var ifEl))
                isField = ifEl.GetBoolean();
            if (args.Value.TryGetProperty("isReadonly", out var irEl))
                isReadonly = irEl.GetBoolean();
            if (args.Value.TryGetProperty("isStatic", out var isEl))
                isStatic = isEl.GetBoolean();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        // Parse attributes
        List<AttributeInfo>? attributes = null;
        if (args?.TryGetProperty("attributes", out var attrEl) == true && attrEl.ValueKind == JsonValueKind.Array)
        {
            attributes = ParseAttributeInfoList(attrEl);
        }

        // Parse documentation
        string? documentation = null;
        if (args?.TryGetProperty("documentation", out var docEl) == true)
        {
            documentation = docEl.GetString();
        }

        var result = await _refactoringService.AddPropertyAsync(new AddPropertyRequest { ClassName = className, PropertyName = propertyName, PropertyType = propertyType, SolutionPath = solutionPath, AccessModifier = accessModifier, HasGetter = hasGetter, HasSetter = hasSetter, HasInit = hasInit, IsRequired = isRequired, InitialValue = initialValue, IsField = isField, IsReadonly = isReadonly, IsStatic = isStatic, Attributes = attributes, Documentation = documentation, Preview = preview }, ct);
        var memberKind = isField ? "field" : "property";
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> AddMethodAsync(JsonElement? args, CancellationToken ct)
    {
        var className = args.GetStringOrDefault("className");
        var methodName = args.GetStringOrDefault("methodName");
        var returnType = args.GetStringOrDefault("returnType");
        var solutionPath = args.GetStringOrDefault("solutionPath");
        List<RefactoringParameterInfo>? parameters = null;
        var accessModifier = "public";
        var isAsync = false;
        var isStatic = false;
        var isExtension = false;
        string? methodModifier = null;
        string? body = null;
        string? testAttribute = null;
        string? documentation = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("parameters", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Array)
            {
                parameters = paramsEl.EnumerateArray().Select(p => new RefactoringParameterInfo(p.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "", p.TryGetProperty("type", out var tp) ? tp.GetString() ?? "" : "", p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null)).ToList();
            }

            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("isAsync", out var asyncEl))
                isAsync = asyncEl.GetBoolean();
            if (args.Value.TryGetProperty("isStatic", out var staticEl))
                isStatic = staticEl.GetBoolean();
            if (args.Value.TryGetProperty("isExtension", out var extEl))
                isExtension = extEl.GetBoolean();
            if (args.Value.TryGetProperty("methodModifier", out var mmEl))
                methodModifier = mmEl.GetString();
            if (args.Value.TryGetProperty("body", out var bodyEl))
                body = bodyEl.GetString();
            if (args.Value.TryGetProperty("testAttribute", out var taEl))
                testAttribute = taEl.GetString();
            if (args.Value.TryGetProperty("documentation", out var docEl))
                documentation = docEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        // Parse generic type parameters for method
        List<TypeParameterInfo>? typeParameters = null;
        if (args?.TryGetProperty("typeParameters", out var tpEl) == true && tpEl.ValueKind == JsonValueKind.Array)
        {
            typeParameters = tpEl.EnumerateArray().Select(tp =>
            {
                var name = tp.TryGetProperty("name", out var n) ? n.GetString() ?? "T" : "T";
                List<string>? constraints = null;
                if (tp.TryGetProperty("constraints", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                {
                    constraints = cEl.EnumerateArray().Select(c => c.GetString()).Where(s => s != null).Cast<string>().ToList();
                }

                return new TypeParameterInfo(name, constraints);
            }).ToList();
        }

        // Parse attributes for method
        List<AttributeInfo>? attributes = null;
        if (args?.TryGetProperty("attributes", out var attrEl) == true && attrEl.ValueKind == JsonValueKind.Array)
        {
            attributes = ParseAttributeInfoList(attrEl);
        }

        var result = await _refactoringService.AddMethodAsync(new AddMethodRequest { ClassName = className, MethodName = methodName, ReturnType = returnType, SolutionPath = solutionPath, Parameters = parameters, AccessModifier = accessModifier, IsAsync = isAsync, IsStatic = isStatic, IsExtension = isExtension, MethodModifier = methodModifier, Body = body, TestAttribute = testAttribute, TypeParameters = typeParameters, Attributes = attributes, Documentation = documentation, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            modifiedFiles = result.ModifiedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    private async Task<object> CreateTypeAsync(JsonElement? args, CancellationToken ct)
    {
        var typeName = args.GetRequiredString("typeName");
        var typeKind = args.GetRequiredString("typeKind");
        var solutionPath = args.GetRequiredString("solutionPath");
        var targetDirectory = args.GetRequiredString("targetDirectory");
        string? ns = null;
        string? baseClass = null;
        List<string>? interfaces = null;
        var accessModifier = "public";
        var isSealed = false;
        var isAbstract = false;
        var isStatic = false;
        var isRecordStruct = false;
        List<string>? additionalUsings = null;
        string? documentationSummary = null;
        var preview = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("namespace", out var nsEl))
                ns = nsEl.GetString();
            if (args.Value.TryGetProperty("baseClass", out var bcEl))
                baseClass = bcEl.GetString();
            if (args.Value.TryGetProperty("implements", out var implEl) && implEl.ValueKind == JsonValueKind.Array)
            {
                interfaces = implEl.EnumerateArray().Select(i => i.GetString()).Where(s => s != null).Cast<string>().ToList();
            }

            if (args.Value.TryGetProperty("accessModifier", out var amEl))
                accessModifier = amEl.GetString() ?? "public";
            if (args.Value.TryGetProperty("isSealed", out var sealedEl))
                isSealed = sealedEl.GetBoolean();
            if (args.Value.TryGetProperty("isAbstract", out var abstractEl))
                isAbstract = abstractEl.GetBoolean();
            if (args.Value.TryGetProperty("isStatic", out var staticEl))
                isStatic = staticEl.GetBoolean();
            if (args.Value.TryGetProperty("isRecordStruct", out var rsEl))
                isRecordStruct = rsEl.GetBoolean();
            if (args.Value.TryGetProperty("additionalUsings", out var usingsEl) && usingsEl.ValueKind == JsonValueKind.Array)
            {
                additionalUsings = usingsEl.EnumerateArray().Select(u => u.GetString()).Where(s => s != null).Cast<string>().ToList();
            }

            if (args.Value.TryGetProperty("documentationSummary", out var docEl))
                documentationSummary = docEl.GetString();
            if (args.Value.TryGetProperty("preview", out var prevEl))
                preview = prevEl.GetBoolean();
        }

        // Parse primary constructor parameters
        List<RefactoringParameterInfo>? primaryConstructorParameters = null;
        if (args?.TryGetProperty("primaryConstructorParameters", out var pcpEl) == true && pcpEl.ValueKind == JsonValueKind.Array)
        {
            primaryConstructorParameters = pcpEl.EnumerateArray().Select(p => new RefactoringParameterInfo(Name: p.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", Type: p.TryGetProperty("type", out var t) ? t.GetString() ?? "object" : "object", DefaultValue: p.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null)).ToList();
        }

        // Parse generic type parameters
        List<TypeParameterInfo>? typeParameters = null;
        if (args?.TryGetProperty("typeParameters", out var tpEl) == true && tpEl.ValueKind == JsonValueKind.Array)
        {
            typeParameters = tpEl.EnumerateArray().Select(tp =>
            {
                var name = tp.TryGetProperty("name", out var n) ? n.GetString() ?? "T" : "T";
                List<string>? constraints = null;
                if (tp.TryGetProperty("constraints", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                {
                    constraints = cEl.EnumerateArray().Select(c => c.GetString()).Where(s => s != null).Cast<string>().ToList();
                }

                return new TypeParameterInfo(name, constraints);
            }).ToList();
        }

        var result = await _refactoringService.CreateTypeAsync(new CreateTypeRequest { TypeName = typeName, TypeKind = typeKind, SolutionPath = solutionPath, TargetDirectory = targetDirectory, Namespace = ns, BaseClass = baseClass, Interfaces = interfaces, AccessModifier = accessModifier, IsSealed = isSealed, IsAbstract = isAbstract, IsStatic = isStatic, IsRecordStruct = isRecordStruct, AdditionalUsings = additionalUsings, DocumentationSummary = documentationSummary, PrimaryConstructorParameters = primaryConstructorParameters, TypeParameters = typeParameters, Preview = preview }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            createdFiles = result.CreatedFiles,
            preview = result.Preview?.Select(p => new { p.FilePath, p.NewContent })
        };
    }

    /// <summary>
    /// aura_generate(operation: "tests") - Generate tests for a target.
    /// </summary>
    private async Task<object> GenerateTestsAsync(JsonElement? args, CancellationToken ct)
    {
        var target = args?.TryGetProperty("target", out var targetEl) == true ? targetEl.GetString() ?? throw new ArgumentException("target is required for tests operation") : args?.TryGetProperty("className", out var classEl) == true ? classEl.GetString() ?? throw new ArgumentException("target or className is required") : throw new ArgumentException("target is required for tests operation");
        var solutionPath = args.GetRequiredString("solutionPath");
        int? count = null;
        int maxTests = 20;
        var focus = TestFocus.All;
        string? testFramework = null;
        bool analyzeOnly = false;
        bool validateCompilation = false;
        if (args.HasValue)
        {
            if (args.Value.TryGetProperty("count", out var countEl))
                count = countEl.GetInt32();
            if (args.Value.TryGetProperty("maxTests", out var maxEl))
                maxTests = maxEl.GetInt32();
            if (args.Value.TryGetProperty("focus", out var focusEl))
            {
                focus = focusEl.GetString() switch
                {
                    "happy_path" => TestFocus.HappyPath,
                    "edge_cases" => TestFocus.EdgeCases,
                    "error_handling" => TestFocus.ErrorHandling,
                    _ => TestFocus.All
                };
            }

            if (args.Value.TryGetProperty("testFramework", out var fwEl))
                testFramework = fwEl.GetString();
            if (args.Value.TryGetProperty("analyzeOnly", out var aoEl))
                analyzeOnly = aoEl.GetBoolean();
            if (args.Value.TryGetProperty("validateCompilation", out var vcEl))
                validateCompilation = vcEl.GetBoolean();
        }

        string? outputDirectory = null;
        if (args?.TryGetProperty("outputDirectory", out var odEl) == true)
            outputDirectory = odEl.GetString();
        var result = await _testGenerationService.GenerateTestsAsync(new TestGenerationRequest { Target = target, SolutionPath = solutionPath, Count = count, MaxTests = maxTests, Focus = focus, TestFramework = testFramework, OutputDirectory = outputDirectory, AnalyzeOnly = analyzeOnly, ValidateCompilation = validateCompilation }, ct);
        return new
        {
            success = result.Success,
            message = result.Message,
            analysis = result.Analysis is not null ? new
            {
                testableMembers = result.Analysis.TestableMembers.Select(m => new { m.Name, m.Signature, m.ReturnType, m.IsAsync, m.ContainingType, parameters = m.Parameters.Select(p => new { p.Name, p.Type, p.IsNullable }), throwsExceptions = m.ThrowsExceptions }),
                existingTests = result.Analysis.ExistingTests.Select(t => new { t.FilePath, t.TestCount, t.TestedMethods }),
                gaps = result.Analysis.Gaps.Select(g => new { g.MethodName, kind = g.Kind.ToString(), g.Description, priority = g.Priority.ToString() }),
                result.Analysis.DetectedFramework,
                result.Analysis.SuggestedTestCount
            }

            : null,
            generated = result.Generated is not null ? new
            {
                result.Generated.TestFilePath,
                result.Generated.FileCreated,
                result.Generated.TestsAdded,
                tests = result.Generated.Tests.Select(t => new { t.TestName, t.Description, t.TargetMethod }),
                compilesSuccessfully = result.Generated.CompilesSuccessfully,
                compilationDiagnostics = result.Generated.CompilationDiagnostics
            }

            : null,
            stoppingReason = result.StoppingReason,
            error = result.Error
        };
    }
}
