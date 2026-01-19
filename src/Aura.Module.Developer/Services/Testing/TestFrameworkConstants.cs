// <copyright file="TestFrameworkConstants.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Module.Developer.Services.Testing;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
/// Shared constants and helpers for test framework detection and code generation.
/// </summary>
public static class TestFrameworkConstants
{
    #region Framework Identifiers

    /// <summary>xUnit framework identifier.</summary>
    public const string FrameworkXUnit = "xunit";

    /// <summary>NUnit framework identifier.</summary>
    public const string FrameworkNUnit = "nunit";

    /// <summary>MSTest framework identifier.</summary>
    public const string FrameworkMsTest = "mstest";

    #endregion

    #region Mocking Library Identifiers

    /// <summary>NSubstitute mocking library identifier.</summary>
    public const string MockLibNSubstitute = "nsubstitute";

    /// <summary>Moq mocking library identifier.</summary>
    public const string MockLibMoq = "moq";

    /// <summary>FakeItEasy mocking library identifier.</summary>
    public const string MockLibFakeItEasy = "fakeiteasy";

    #endregion

    #region Namespace Names

    /// <summary>NSubstitute namespace.</summary>
    public const string NsNSubstitute = "NSubstitute";

    /// <summary>Moq namespace.</summary>
    public const string NsMoq = "Moq";

    /// <summary>FakeItEasy namespace.</summary>
    public const string NsFakeItEasy = "FakeItEasy";

    #endregion

    #region Test Attribute Names

    /// <summary>xUnit Fact attribute.</summary>
    public const string AttrFact = "Fact";

    /// <summary>xUnit Theory attribute.</summary>
    public const string AttrTheory = "Theory";

    /// <summary>NUnit Test attribute.</summary>
    public const string AttrTest = "Test";

    /// <summary>NUnit TestCase attribute.</summary>
    public const string AttrTestCase = "TestCase";

    /// <summary>MSTest TestMethod attribute.</summary>
    public const string AttrTestMethod = "TestMethod";

    /// <summary>MSTest DataTestMethod attribute.</summary>
    public const string AttrDataTestMethod = "DataTestMethod";

    #endregion

    #region Project Classification

    /// <summary>
    /// Determines if a project is a test project based on naming conventions.
    /// </summary>
    public static bool IsTestProject(Project project) =>
        project.Name.Contains("Test", StringComparison.OrdinalIgnoreCase) ||
        project.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if a project is a unit test project (excludes integration tests).
    /// </summary>
    public static bool IsUnitTestProject(Project project) =>
        IsTestProject(project) &&
        !project.Name.Contains("Integration", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if a class is a test class based on naming conventions.
    /// </summary>
    public static bool IsTestClass(string className) =>
        className.EndsWith("Tests", StringComparison.Ordinal) ||
        className.EndsWith("Test", StringComparison.Ordinal);

    #endregion

    #region Framework Detection

    /// <summary>
    /// Detects the test framework from project metadata references.
    /// </summary>
    public static string DetectFrameworkFromReferences(IEnumerable<MetadataReference> references)
    {
        var refs = references.Select(r => r.Display ?? "").ToList();

        if (refs.Any(r => r.Contains("xunit", StringComparison.OrdinalIgnoreCase)))
            return FrameworkXUnit;
        if (refs.Any(r => r.Contains("nunit", StringComparison.OrdinalIgnoreCase)))
            return FrameworkNUnit;
        if (refs.Any(r => r.Contains("MSTest", StringComparison.OrdinalIgnoreCase)))
            return FrameworkMsTest;

        return FrameworkXUnit; // default
    }

    /// <summary>
    /// Detects the test framework from class method attributes.
    /// </summary>
    public static string? DetectFrameworkFromAttributes(ClassDeclarationSyntax classNode)
    {
        if (!IsTestClass(classNode.Identifier.Text))
        {
            return null;
        }

        var allAttributes = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists)
            .SelectMany(al => al.Attributes)
            .Select(a => a.Name.ToString())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // xUnit
        if (allAttributes.Any(a => a is AttrFact or AttrFact + "Attribute" or AttrTheory or AttrTheory + "Attribute"))
        {
            return FrameworkXUnit;
        }

        // NUnit
        if (allAttributes.Any(a => a is AttrTest or AttrTest + "Attribute" or AttrTestCase or AttrTestCase + "Attribute"))
        {
            return FrameworkNUnit;
        }

        // MSTest
        if (allAttributes.Any(a => a is AttrTestMethod or AttrTestMethod + "Attribute" or AttrDataTestMethod or AttrDataTestMethod + "Attribute"))
        {
            return FrameworkMsTest;
        }

        // Test class but can't detect framework
        return FrameworkXUnit;
    }

    /// <summary>
    /// Detects the mocking library from project metadata references.
    /// </summary>
    public static string DetectMockingLibraryFromReferences(IEnumerable<MetadataReference> references)
    {
        var refs = references.Select(r => r.Display ?? "").ToList();

        if (refs.Any(r => r.Contains("NSubstitute", StringComparison.OrdinalIgnoreCase)))
            return MockLibNSubstitute;
        if (refs.Any(r => r.Contains("Moq", StringComparison.OrdinalIgnoreCase) &&
                     !r.Contains("NSubstitute", StringComparison.OrdinalIgnoreCase)))
            return MockLibMoq;
        if (refs.Any(r => r.Contains("FakeItEasy", StringComparison.OrdinalIgnoreCase)))
            return MockLibFakeItEasy;

        return MockLibNSubstitute; // default
    }

    /// <summary>
    /// Gets the namespace for a mocking library.
    /// </summary>
    public static string? GetMockingLibraryNamespace(string mockingLibrary) =>
        mockingLibrary switch
        {
            MockLibNSubstitute => NsNSubstitute,
            MockLibMoq => NsMoq,
            MockLibFakeItEasy => NsFakeItEasy,
            _ => null
        };

    /// <summary>
    /// Gets the test attribute name for a framework.
    /// </summary>
    public static string GetTestAttributeName(string frameworkOrAttribute) =>
        frameworkOrAttribute.ToLowerInvariant() switch
        {
            FrameworkXUnit => AttrFact,
            FrameworkNUnit => AttrTest,
            FrameworkMsTest => AttrTestMethod,
            // Handle direct attribute names
            "fact" => AttrFact,
            "theory" => AttrTheory,
            "test" => AttrTest,
            "testcase" => AttrTestCase,
            "testmethod" => AttrTestMethod,
            "datatestmethod" => AttrDataTestMethod,
            // Unknown - use as-is
            _ => frameworkOrAttribute
        };

    /// <summary>
    /// Infers framework from attribute name.
    /// </summary>
    public static string InferFrameworkFromAttribute(string attribute) =>
        attribute.ToLowerInvariant() switch
        {
            "fact" or "theory" => FrameworkXUnit,
            "test" or "testcase" => FrameworkNUnit,
            "testmethod" or "datatestmethod" => FrameworkMsTest,
            _ => FrameworkXUnit
        };

    #endregion
}
