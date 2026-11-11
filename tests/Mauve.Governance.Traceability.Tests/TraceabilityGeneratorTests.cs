using Mauve.Governance.Traceability;
using Xunit;

namespace Mauve.Governance.Traceability.Tests;

public sealed class TraceabilityGeneratorTests
{
    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void NativeFrameworkAnnotationsAndParameterizedCases()
    {
        var report = Generate(
            ("a.cs", """
                using Microsoft.VisualStudio.TestTools.UnitTesting;
                namespace App;
                public class Ms {
                    [DataTestMethod]
                    [TestProperty("Feature", "FEAT-01")]
                    [TestProperty("Requirement", "FEAT-01-A")]
                    [DataRow(1)] [DataRow(2)]
                    public void Works(int value) { }
                }
                """),
            ("b.cs", """
                using Xunit;
                using Mauve.Governance.Testing;
                namespace App;
                public class XunitTests {
                    [Theory]
                    [Trait(TestMetadata.Feature, "FEAT-01")]
                    [Trait(TestMetadata.Requirement, "FEAT-01-A")]
                    [InlineData("one", 1)] [InlineData("two", 2)]
                    [MemberData(nameof(More))]
                    public void Works(string text, int value) { }
                }
                """),
            ("c.cs", """
                using NUnit.Framework;
                namespace App;
                class NUnitTests {
                    [TestCase(1)] [TestCaseSource(nameof(Cases))]
                    [Property("Feature", "FEAT-02")]
                    [Property("Requirement", "FEAT-02-A")]
                    public void Works(int x) { }
                }
                """));

        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal(3, report.Tests.Count);
        Assert.Equal(new[] { "MSTest", "NUnit", "xUnit v2/v3" },
            report.Tests.Select(t => t.Framework).Order(StringComparer.Ordinal));
        Assert.Equal(2, report.Tests[0].Cases.Count);
        Assert.Equal(2, report.Tests[1].Cases.Count);
        Assert.Equal(3, report.Tests[2].Cases.Count);
        Assert.Equal("DynamicSource", report.Tests[2].Cases[2].Kind);
        Assert.Equal(5, report.Features[0].Cases);
        Assert.Equal("FEAT-01-A", report.Features[0].Requirements.Single().Id);
        Assert.Equal(5, report.Features[0].Requirements.Single().Cases);
        Assert.Equal(2, report.Features[1].Cases);
        Assert.Contains("not test discovery, execution evidence", report.EvidenceDisclaimer);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void ConvenienceAttributesAndQualifiedAliasNamesWorkWithoutTheirAssemblies()
    {
        var report = Generate(
            ("v2.cs", """
                using Xunit;
                using Mauve.Governance.Testing.XunitV2;
                class V2 {
                    [Fact] [Feature("FEAT-1")] [Requirement("FEAT-1-R")]
                    void Test() {}
                }
                """),
            ("v3.cs", """
                using Xunit;
                using G = Mauve.Governance.Testing.XunitV3;
                class V3 {
                    [Fact] [G.FeatureAttribute("FEAT-1")]
                    [G.Requirement("FEAT-1-R")] void Test() {}
                }
                """),
            ("nunit.cs", """
                using NUnit.Framework;
                using Mauve.Governance.Testing.NUnit;
                class N {
                    [Test] [Feature("FEAT-1")] [Requirement("FEAT-1-R")]
                    void Test() {}
                }
                """));

        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal(3, report.Features[0].Cases);
        Assert.Equal(3, report.Features[0].Requirements[0].Tests.Count);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void RejectsUnannotatedDuplicateInvalidAndCrossFeatureMetadata()
    {
        var report = Generate(("cases.cs", """
            using Xunit;
            class C {
                [Fact] public void Missing() {}
                [Theory]
                [Trait("Feature", "FEAT-1")] [Trait("Feature", "FEAT-2")]
                [Trait("Requirement", "FEAT-1-A")] [Trait("Requirement", "FEAT-1-A")]
                [InlineData(1)] [InlineData(1)]
                public void Duplicate(int value) {}
                [Fact] [Trait("Feature", "FEAT-1")]
                [Trait("Requirement", "FEAT-2-R")] public void Cross() {}
                [Fact] [Trait("Feature", "bad")]
                [Trait("Requirement", "bad")] public void Invalid() {}
                [Fact] [Trait("Feature", Constants.Id)] public void NonLiteral() {}
                [Fact] [Trait("Requirement", "FEAT-1-R")]
                public void MissingDespiteRequirement() {}
            }
            """));

        Assert.False(report.IsValid);
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE002" && d.Message.Contains("Missing()", StringComparison.Ordinal));
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE003" && d.Message.Contains("Feature", StringComparison.Ordinal));
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE003" && d.Message.Contains("Requirement", StringComparison.Ordinal));
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE004");
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE005");
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE006");
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE009");
        Assert.All(report.Diagnostics, d => Assert.Equal("cases.cs", d.Path));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void IgnoresClassLevelMetadataAndNonTestMethods()
    {
        var report = Generate(("source.cs", """
            using Xunit;
            [Trait("Feature", "FEAT-1")]
            class C {
                [Fact] public void Test() {}
                [Trait("Feature", "FEAT-1")] void Helper() {}
                // [Fact] [Trait("Feature", "FEAT-3")]
                const string Description = "[Fact]";
            }
            """));

        Assert.Single(report.Tests);
        Assert.Equal("TRACE002", Assert.Single(report.Diagnostics).Code);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void StableJsonAndDuplicateIdentities()
    {
        var first = new TraceabilitySource("z.cs", """
            using Xunit; namespace N; partial class C {
                [Fact] [Trait("Feature", "FEAT-2")] void Same() {}
            }
            """);
        var second = new TraceabilitySource("a.cs", """
            using Xunit; namespace N; partial class C {
                [Fact] [Trait("Feature", "FEAT-2")] void Same() {}
            }
            """);
        var report = TraceabilityGenerator.Generate([first, second]);

        Assert.Equal(report.ToJson(), TraceabilityGenerator.Generate([second, first]).ToJson());
        Assert.Equal(2, report.Diagnostics.Count(d => d.Code == "TRACE007"));
        Assert.Contains("\"schemaVersion\": 1", report.ToJson());
        Assert.Contains("\"evidenceDisclaimer\"", report.ToJson());
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void SourceCommentsAndLineShiftsDoNotChangeTheIndex()
    {
        const string source = """
            using Xunit;
            class CheckoutTests {
                [Theory]
                [Trait("Feature", "FEAT-Checkout")]
                [Trait("Requirement", "FEAT-Checkout-001")]
                [InlineData(1)]
                void RejectsExpiredToken(int value) {}
            }
            """;
        var original = Generate(("checkout.cs", source));
        var withComments = Generate(("checkout.cs", source
            .Replace("[InlineData(1)]", "[InlineData(1 /* explanation */)]", StringComparison.Ordinal)
            .Replace("    [Theory]", "    // explanation\n\n    [Theory]", StringComparison.Ordinal)));

        Assert.True(original.IsValid, original.ToJson());
        Assert.True(withComments.IsValid, withComments.ToJson());
        Assert.NotEqual(original.Tests[0].Line, withComments.Tests[0].Line);
        Assert.Equal(original.ToJson(), withComments.ToJson());
        Assert.DoesNotContain("\"line\":", original.ToJson());

        var changedFeature = Generate(("checkout.cs",
            source.Replace("FEAT-Checkout-001", "FEAT-Checkout-002", StringComparison.Ordinal)));
        Assert.NotEqual(original.ToJson(), changedFeature.ToJson());
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void RejectsMalformedSyntaxAndDuplicateInputPaths()
    {
        var report = TraceabilityGenerator.Generate([
            new("broken.cs", "using Xunit; class C { [Fact] void Broken( {"),
            new("broken.cs", "using Xunit;")
        ]);

        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE001");
        Assert.Contains(report.Diagnostics, d => d.Code == "TRACE008");
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void GlobalAndProjectImplicitUsingsAreSupported()
    {
        var sources = new[]
        {
            new TraceabilitySource("imports.cs", """
                global using Xunit;
                global using G = Mauve.Governance.Testing.XunitV2;
                """),
            new TraceabilitySource("tests.cs", """
                class C {
                    [Fact] [G.Feature("FEAT-7")] [G.Requirement("FEAT-7-R")]
                    void Test() {}
                }
                """)
        };
        Assert.True(TraceabilityGenerator.Generate(sources).IsValid);
        Assert.True(TraceabilityGenerator.Generate([new("implicit.cs", """
            class C {
                [Fact] [Trait("Feature", "FEAT-7")]
                void Test() {}
            }
            """)], ["Xunit"]).IsValid);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void FullyQualifiedAttributesAndUnresolvedTheoryRows()
    {
        var report = Generate(("qualified.cs", """
            namespace Tests {
                class C<T> {
                    [global::Xunit.TheoryAttribute]
                    [global::Xunit.TraitAttribute("Feature", "FEAT-8")]
                    [global::Xunit.TraitAttribute("Requirement", "FEAT-8-Case")]
                    void Parameterized(T x) {}
                }
            }
            """));

        Assert.True(report.IsValid, report.ToJson());
        Assert.Equal("Tests.C`1.Parameterized(T)", report.Tests[0].Id);
        Assert.Equal("Undetermined", report.Tests[0].Cases.Single().Kind);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanRejectsOversizedFile()
    {
        InFixtureDirectory(root =>
        {
            File.WriteAllText(Path.Combine(root, "large.cs"),
                new string('a', TraceabilityGenerator.MaximumSourceFileBytes + 1));
            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanRejectsExcessiveDepth()
    {
        InFixtureDirectory(root =>
        {
            var nested = root;
            for (var i = 0; i < TraceabilityGenerator.MaximumDirectoryDepth; i++)
            {
                nested = Path.Combine(nested, "child");
                Directory.CreateDirectory(nested);
            }

            File.WriteAllText(Path.Combine(nested, "test.cs"), "class Test {}");
            Assert.True(TraceabilityGenerator.GenerateFromDirectory(root).IsValid);
            nested = Path.Combine(nested, "child");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "test.cs"), "class Test {}");
            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanRejectsExcessiveTotalBytes()
    {
        InFixtureDirectory(root =>
        {
            var content = new string('a', TraceabilityGenerator.MaximumSourceFileBytes);
            var number = TraceabilityGenerator.MaximumTotalSourceBytes /
                TraceabilityGenerator.MaximumSourceFileBytes;
            for (var i = 0; i <= number; i++)
            {
                File.WriteAllText(Path.Combine(root, $"test{i:D4}.cs"), content);
            }

            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanRejectsExcessiveFileCount()
    {
        InFixtureDirectory(root =>
        {
            for (var i = 0; i <= TraceabilityGenerator.MaximumSourceFiles; i++)
            {
                File.WriteAllText(Path.Combine(root, $"test{i:D4}.cs"), "");
            }

            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanHonorsProjectUsingsAndDoesNotLeakThemAcrossProjects()
    {
        InFixtureDirectory(root =>
        {
            var first = Path.Combine(root, "first");
            var nested = Path.Combine(first, "nested");
            var second = Path.Combine(root, "second");
            Directory.CreateDirectory(nested);
            Directory.CreateDirectory(second);
            File.WriteAllText(Path.Combine(first, "first.csproj"),
                """<Project><ItemGroup><Using Include="Xunit" /></ItemGroup></Project>""");
            File.WriteAllText(Path.Combine(nested, "test.cs"),
                """
                class Scoped {
                    [Fact] [Trait("Feature", "FEAT-01")]
                    void Found() {}
                }
                """);
            File.WriteAllText(Path.Combine(second, "second.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(second, "test.cs"),
                """
                class Other {
                    [Fact] [Trait("Feature", "FEAT-02")]
                    void Ambiguous() {}
                }
                """);

            var report = TraceabilityGenerator.GenerateFromDirectory(root);
            Assert.Equal(2, report.Tests.Count);
            Assert.Equal("FEAT-01", Assert.Single(report.Tests, t => t.Path.StartsWith("first/", StringComparison.Ordinal)).Feature);
            Assert.Contains(report.Diagnostics, d => d.Code == "TRACE010" && d.Path == "second/test.cs");
            Assert.DoesNotContain(report.Diagnostics, d => d.Code == "TRACE010" && d.Path == "first/nested/test.cs");
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanScopesSourceGlobalUsingsToTheirProject()
    {
        InFixtureDirectory(root =>
        {
            foreach (var name in new[] { "first", "second" })
            {
                var dir = Path.Combine(root, name);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, name + ".csproj"), "<Project />");
            }

            File.WriteAllText(Path.Combine(root, "first", "global.cs"), "global using Xunit;");
            File.WriteAllText(Path.Combine(root, "first", "test.cs"),
                """class First { [Fact] [Trait("Feature", "FEAT-01")] void Found() {} }""");
            File.WriteAllText(Path.Combine(root, "second", "test.cs"),
                """class Second { [Fact] [Trait("Feature", "FEAT-02")] void Ambiguous() {} }""");

            var report = TraceabilityGenerator.GenerateFromDirectory(root);
            Assert.Equal(2, report.Tests.Count);
            Assert.Contains(report.Diagnostics, d => d.Code == "TRACE010" && d.Path == "second/test.cs");
            Assert.DoesNotContain(report.Diagnostics, d => d.Code == "TRACE010" && d.Path == "first/test.cs");
        });
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void DirectoryScanRejectsUnboundedOrConditionalProjectImports()
    {
        InFixtureDirectory(root =>
        {
            var project = Path.Combine(root, "test.csproj");
            File.WriteAllText(project,
                """<Project><ItemGroup><Using Include="Xunit" Condition="'$(IsTest)' == 'true'" /></ItemGroup></Project>""");
            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
            File.WriteAllText(project, new string(' ', TraceabilityGenerator.MaximumProjectFileBytes + 1));
            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
            File.WriteAllText(project,
                "<!DOCTYPE p [<!ENTITY e \"Xunit\">]><Project><Using Include=\"&e;\" /></Project>");
            Assert.Throws<InvalidDataException>(() => TraceabilityGenerator.GenerateFromDirectory(root));
        });
    }

    private static void InFixtureDirectory(Action<string> verify)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(),
            ".traceability-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            verify(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TraceabilityReport Generate(params (string Path, string Text)[] sources) =>
        TraceabilityGenerator.Generate(sources.Select(s => new TraceabilitySource(s.Path, s.Text)));
}
