using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Mauve.Governance.Testing.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TestMetadataAnalyzer : DiagnosticAnalyzer
{
    public const string MissingFeatureId = "MGTEST001";
    public const string InvalidFeatureId = "MGTEST002";
    public const string InvalidRequirementId = "MGTEST003";
    public const string ConflictingMetadataId = "MGTEST004";

    private static readonly DiagnosticDescriptor MissingFeature = Rule(
        MissingFeatureId, "Missing feature", "Test '{0}' must have one Feature metadata value");
    private static readonly DiagnosticDescriptor InvalidFeature = Rule(
        InvalidFeatureId, "Invalid feature", "Feature '{0}' must be a valid FEAT- identifier");
    private static readonly DiagnosticDescriptor InvalidRequirement = Rule(
        InvalidRequirementId, "Invalid requirement", "Requirement '{0}' must belong to the test's feature");
    private static readonly DiagnosticDescriptor ConflictingMetadata = Rule(
        ConflictingMetadataId, "Duplicate metadata", "Test '{0}' has duplicate or conflicting {1} metadata");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingFeature, InvalidFeature, InvalidRequirement, ConflictingMetadata);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private static DiagnosticDescriptor Rule(string id, string title, string message) =>
        new DiagnosticDescriptor(id, title, message, "Governance", DiagnosticSeverity.Warning, true);

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (method.MethodKind != MethodKind.Ordinary ||
            !method.GetAttributes().Any(a => IsTest(a.AttributeClass)))
        {
            return;
        }

        var location = method.Locations.FirstOrDefault(l => l.IsInSource);
        if (location == null)
        {
            return;
        }

        var featureValues = new List<string>();
        var requirementValues = new List<string>();
        foreach (var attribute in method.GetAttributes())
        {
            if (!TryMetadata(attribute, out var key, out var value))
            {
                continue;
            }

            if (key == "Feature")
            {
                featureValues.Add(value);
            }
            else if (key == "Requirement")
            {
                requirementValues.Add(value);
            }
        }

        if (featureValues.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingFeature, location, method.Name));
        }
        else
        {
            if (featureValues.Count != 1)
            {
                context.ReportDiagnostic(Diagnostic.Create(ConflictingMetadata, location, method.Name, "Feature"));
            }

            foreach (var feature in featureValues.Where(f => !ValidId(f, "FEAT-")))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidFeature, location, feature));
            }
        }

        var owner = featureValues.Count == 1 && ValidId(featureValues[0], "FEAT-")
            ? featureValues[0] : null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in requirementValues)
        {
            if (!seen.Add(requirement))
            {
                context.ReportDiagnostic(Diagnostic.Create(ConflictingMetadata, location, method.Name, "Requirement"));
            }

            if (!ValidId(requirement, "FEAT-") ||
                requirement.IndexOf('-', 5) < 6 ||
                owner != null && (!requirement.StartsWith(owner + "-", StringComparison.Ordinal) ||
                    !ValidId(requirement.Substring(owner.Length + 1), "")))
            {
                context.ReportDiagnostic(Diagnostic.Create(InvalidRequirement, location, requirement));
            }
        }
    }

    private static bool ValidId(string id, string prefix)
    {
        if (!id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = id.Substring(prefix.Length);
        return suffix.Length > 0 && IsAsciiAlphanumeric(suffix[0]) &&
            IsAsciiAlphanumeric(suffix[suffix.Length - 1]) &&
            suffix.All(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' ||
                c >= '0' && c <= '9' || c == '-' || c == '_' || c == '.');
    }

    private static bool IsAsciiAlphanumeric(char c) =>
        c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9';

    private static bool IsTest(INamedTypeSymbol? type) =>
        Derives(type, "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute") ||
        Derives(type, "Xunit.FactAttribute") ||
        Derives(type, "NUnit.Framework.TestAttribute") ||
        Derives(type, "NUnit.Framework.TestCaseAttribute") ||
        Derives(type, "NUnit.Framework.TestCaseSourceAttribute") ||
        Derives(type, "NUnit.Framework.TheoryAttribute");

    private static bool TryMetadata(AttributeData attribute, out string key, out string value)
    {
        key = "";
        value = "";
        var type = attribute.AttributeClass;
        if (type != null && IsGovernanceWrapper(type, out key) &&
            attribute.ConstructorArguments.Length == 1 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive)
        {
            value = attribute.ConstructorArguments[0].Value as string ?? "";
            return true;
        }

        if (!Derives(type, "Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute") &&
            !Derives(type, "Xunit.TraitAttribute") &&
            !Derives(type, "NUnit.Framework.PropertyAttribute"))
        {
            return false;
        }

        if (attribute.ConstructorArguments.Length >= 2 &&
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
            attribute.ConstructorArguments[1].Kind == TypedConstantKind.Primitive &&
            attribute.ConstructorArguments[0].Value is string name)
        {
            key = name;
            value = attribute.ConstructorArguments[1].Value as string ?? "";
            return true;
        }

        return false;
    }

    private static bool IsGovernanceWrapper(INamedTypeSymbol type, out string key)
    {
        var name = type.ToDisplayString();
        foreach (var framework in new[] { "XunitV2", "XunitV3", "NUnit" })
        {
            var prefix = "Mauve.Governance.Testing." + framework + ".";
            if (name == prefix + "FeatureAttribute")
            {
                key = "Feature";
                return true;
            }

            if (name == prefix + "RequirementAttribute")
            {
                key = "Requirement";
                return true;
            }
        }

        key = "";
        return false;
    }

    private static bool Derives(INamedTypeSymbol? type, string fullName)
    {
        for (; type != null; type = type.BaseType)
        {
            if (type.ToDisplayString() == fullName)
            {
                return true;
            }
        }

        return false;
    }
}
