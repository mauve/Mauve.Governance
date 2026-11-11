using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Mauve.Governance.Testing.Analyzers;
using Xunit;

namespace Mauve.Governance.Testing.Analyzers.Tests;

public sealed class TestMetadataAnalyzerTests
{
    private const string Frameworks = """
        namespace Microsoft.VisualStudio.TestTools.UnitTesting {
            public class TestMethodAttribute : System.Attribute {}
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class TestPropertyAttribute : System.Attribute {
                public TestPropertyAttribute(string name, string value) {}
            }
        }
        namespace Xunit {
            public class FactAttribute : System.Attribute {}
            public class TheoryAttribute : FactAttribute {}
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class TraitAttribute : System.Attribute {
                public TraitAttribute(string name, string value) {}
            }
            public class CustomTraitAttribute : TraitAttribute {
                public CustomTraitAttribute(string name, string value) : base(name, value) {}
            }
        }
        namespace NUnit.Framework {
            public class TestAttribute : System.Attribute {}
            public class TestCaseAttribute : System.Attribute {
                public TestCaseAttribute(int value) {}
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class PropertyAttribute : System.Attribute {
                public PropertyAttribute(string name, string value) {}
            }
        }
        namespace Mauve.Governance.Testing.XunitV2 {
            public class FeatureAttribute : System.Attribute {
                public FeatureAttribute(string id) {}
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class RequirementAttribute : System.Attribute {
                public RequirementAttribute(string id) {}
            }
        }
        namespace Mauve.Governance.Testing.XunitV3 {
            public class FeatureAttribute : System.Attribute {
                public FeatureAttribute(string id) {}
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class RequirementAttribute : System.Attribute {
                public RequirementAttribute(string id) {}
            }
        }
        namespace Mauve.Governance.Testing.NUnit {
            public class FeatureAttribute : System.Attribute {
                public FeatureAttribute(string id) {}
            }
            [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
            public class RequirementAttribute : System.Attribute {
                public RequirementAttribute(string id) {}
            }
        }
        """;

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Theory]
    [InlineData("Microsoft.VisualStudio.TestTools.UnitTesting", "TestMethod", "TestProperty")]
    [InlineData("Xunit", "Fact", "Trait")]
    [InlineData("NUnit.Framework", "Test", "Property")]
    public async Task NativeAttributesAndFlexibleIdsAreAccepted(string ns, string test, string property)
    {
        var source = $$"""
            using {{ns}};
            class Other {
                [{{test}}]
                [{{property}}("Feature", "FEAT-Checkout-Legacy")]
                [{{property}}("Requirement", "FEAT-Checkout-Legacy-Sale_001")]
                void Valid() {}
            }
            """;
        Assert.Empty(await Analyze(source));
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Theory]
    [InlineData("Xunit", "Fact", "XunitV2")]
    [InlineData("Xunit", "Fact", "XunitV3")]
    [InlineData("NUnit.Framework", "Test", "NUnit")]
    public async Task ConvenienceAttributesAreAccepted(string ns, string test, string adapter)
    {
        var source = $$"""
            using {{ns}};
            class T {
                [{{test}}]
                [Mauve.Governance.Testing.{{adapter}}.Feature("FEAT-Checkout")]
                [Mauve.Governance.Testing.{{adapter}}.Requirement("FEAT-Checkout-1")]
                void Valid() {}
            }
            """;
        Assert.Empty(await Analyze(source));
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Fact]
    public async Task ConvenienceAndNativeDuplicateFeatureIsDiagnosed()
    {
        var results = await Analyze("""
            using Xunit;
            class T {
                [Fact]
                [Trait("Feature", "FEAT-A")]
                [Mauve.Governance.Testing.XunitV2.Feature("FEAT-A")]
                void Invalid() {}
            }
            """);
        Assert.Contains(results, d => d.Id == TestMetadataAnalyzer.ConflictingMetadataId);
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Fact]
    public async Task ConvenienceCrossFeatureRequirementIsDiagnosed()
    {
        var results = await Analyze("""
            using NUnit.Framework;
            class T {
                [Test]
                [Mauve.Governance.Testing.NUnit.Feature("FEAT-A")]
                [Mauve.Governance.Testing.NUnit.Requirement("FEAT-B-001")]
                void Invalid() {}
            }
            """);
        Assert.Contains(results, d => d.Id == TestMetadataAnalyzer.InvalidRequirementId);
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Theory]
    [InlineData("[Fact]", TestMetadataAnalyzer.MissingFeatureId)]
    [InlineData("[Fact][Trait(\"Feature\", \"bad\")]", TestMetadataAnalyzer.InvalidFeatureId)]
    [InlineData("[Fact][Trait(\"Feature\", null)]", TestMetadataAnalyzer.InvalidFeatureId)]
    [InlineData("[Fact][Trait(\"Feature\", \"FEAT-A\")][Trait(\"Requirement\", \"FEAT-B-001\")]", TestMetadataAnalyzer.InvalidRequirementId)]
    [InlineData("[Fact][Trait(\"Feature\", \"FEAT-A\")][Trait(\"Requirement\", \"FEAT-A-\")]", TestMetadataAnalyzer.InvalidRequirementId)]
    [InlineData("[Fact][Trait(\"Feature\", \"FEAT-A\")][Trait(\"Feature\", \"FEAT-B\")]", TestMetadataAnalyzer.ConflictingMetadataId)]
    [InlineData("[Fact][Trait(\"Feature\", \"FEAT-A\")][Trait(\"Requirement\", \"FEAT-A-001\")][Trait(\"Requirement\", \"FEAT-A-001\")]", TestMetadataAnalyzer.ConflictingMetadataId)]
    public async Task InvalidMetadataIsDiagnosed(string attributes, string diagnostic)
    {
        var results = await Analyze($"using Xunit; class T {{ {attributes} void Broken() {{}} }}");
        Assert.Contains(results, d => d.Id == diagnostic);
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Fact]
    public async Task TwoArgumentNativeDerivedAttributesAreAccepted()
    {
        Assert.Empty(await Analyze("""
            using Xunit;
            class T {
                [Fact]
                [CustomTrait("Feature", "FEAT-Alpha")]
                [CustomTrait("Requirement", "FEAT-Alpha-1")]
                void Valid() {}
            }
            """));
    }

    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-001")]
    [Fact]
    public async Task NonTestsAreIgnored()
    {
        Assert.Empty(await Analyze("class T { void Helper() {} }"));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create("Fixture",
            [CSharpSyntaxTree.ParseText(source + "\n" + Frameworks)],
            platform.Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers([new TestMetadataAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
