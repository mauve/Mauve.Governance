using System.Text.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mauve.Governance.Traceability;

public sealed record TraceabilitySource(string Path, string Text);
public sealed record TraceabilityDiagnostic(string Code, string Path, int Line, string Message);
public sealed record TraceabilityCase(string Id, string Kind, string Attribute, IReadOnlyList<string> Arguments);
public sealed record TraceabilityTest(
    string Id, string Framework, string Path,
    [property: System.Text.Json.Serialization.JsonIgnore] int Line, string? Feature,
    IReadOnlyList<string> Requirements, IReadOnlyList<TraceabilityCase> Cases);
public sealed record TraceabilityRequirement(string Id, IReadOnlyList<string> Tests, int Cases);
public sealed record TraceabilityFeature(
    string Id, IReadOnlyList<string> Tests, int Cases, IReadOnlyList<TraceabilityRequirement> Requirements);

/// <summary>A static source index, not evidence that any test was discovered or executed by a runner.</summary>
public sealed record TraceabilityReport(
    int SchemaVersion, string EvidenceDisclaimer, IReadOnlyList<TraceabilityTest> Tests,
    IReadOnlyList<TraceabilityFeature> Features, IReadOnlyList<TraceabilityDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    }) + "\n";
}

/// <summary>
/// Reads C# source syntax only. Attribute types and constants are recognized lexically; no
/// compilation, reflection, assembly loading, runner discovery or runtime data enumeration occurs.
/// </summary>
public static class TraceabilityGenerator
{
    public const int MaximumSourceFileBytes = 1024 * 1024;
    public const int MaximumTotalSourceBytes = 32 * 1024 * 1024;
    public const int MaximumSourceFiles = 4096;
    public const int MaximumDirectoryDepth = 32;
    public const int MaximumProjectFileBytes = 128 * 1024;
    public const int MaximumProjectFiles = 512;

    private const string Disclaimer =
        "Source-only annotation index; not test discovery, execution evidence, coverage, or a TRX report. " +
        "Case counts represent source case descriptors, not executed invocations; dynamic data " +
        "sources, custom-derived attributes and non-literal metadata cannot be evaluated without compilation or running code.";

    private static readonly StringComparer Order = StringComparer.Ordinal;

    /// <summary>Generate a report from explicitly supplied paths and source text; paths must be unique.</summary>
    public static TraceabilityReport Generate(
        IEnumerable<TraceabilitySource> sources, IEnumerable<string>? implicitUsings = null)
        => GenerateCore(sources, implicitUsings, new Dictionary<string, string[]>(Order),
            new Dictionary<string, string>(Order));

    private static TraceabilityReport GenerateCore(
        IEnumerable<TraceabilitySource> sources, IEnumerable<string>? implicitUsings,
        IReadOnlyDictionary<string, string[]> projectUsings, IReadOnlyDictionary<string, string> projectScopes)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var files = sources.OrderBy(s => s.Path, Order).ToArray();
        var tests = new List<TraceabilityTest>();
        var diagnostics = new List<TraceabilityDiagnostic>();
        var paths = new HashSet<string>(Order);
        var globalImports = files.GroupBy(s => projectScopes.GetValueOrDefault(s.Path) ?? "", Order)
            .ToDictionary(g => g.Key,
                g => g.SelectMany(s => CSharpSyntaxTree.ParseText(s.Text).GetCompilationUnitRoot()
                    .Usings.Where(u => u.GlobalKeyword != default)).ToArray(), Order);
        var injectedImports = (implicitUsings ?? Array.Empty<string>()).ToArray();
        foreach (var source in files)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.Path);
            ArgumentNullException.ThrowIfNull(source.Text);
            if (!paths.Add(source.Path))
            {
                diagnostics.Add(new("TRACE008", source.Path, 0, "Duplicate source path."));
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(source.Text, path: source.Path);
            var root = tree.GetCompilationUnitRoot();
            var sourceImports = injectedImports
                .Concat(projectUsings.GetValueOrDefault(source.Path) ?? [])
                .ToArray();
            foreach (var error in tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                diagnostics.Add(new("TRACE001", source.Path, error.Location.GetLineSpan().StartLinePosition.Line + 1,
                    "C# source syntax error: " + error.GetMessage()));
            }

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var imports = root.Usings.Concat(method.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>().Reverse()
                    .SelectMany(n => n.Usings))
                    .Concat(globalImports[projectScopes.GetValueOrDefault(source.Path) ?? ""]).ToArray();
                ProcessMethod(method, imports, sourceImports, source.Path, tests, diagnostics);
            }
        }

        foreach (var group in tests.GroupBy(t => t.Id, Order).Where(g => g.Count() > 1))
        {
            foreach (var test in group)
            {
                diagnostics.Add(new("TRACE007", test.Path, test.Line, $"Duplicate test identity '{test.Id}'."));
            }
        }

        var sortedTests = tests.OrderBy(t => t.Id, Order).ThenBy(t => t.Path, Order)
            .ThenBy(t => t.Line).ToArray();
        var features = sortedTests.Where(t => t.Feature != null)
            .GroupBy(t => t.Feature!, Order)
            .OrderBy(g => g.Key, Order)
            .Select(g => new TraceabilityFeature(g.Key, g.Select(t => t.Id).Distinct(Order).ToArray(),
                g.Sum(t => t.Cases.Count),
                g.SelectMany(t => t.Requirements.Select(r => (Requirement: r, Test: t)))
                    .GroupBy(x => x.Requirement, Order).OrderBy(r => r.Key, Order)
                    .Select(r => new TraceabilityRequirement(r.Key,
                        r.Select(x => x.Test.Id).Distinct(Order).OrderBy(id => id, Order).ToArray(),
                        r.Sum(x => x.Test.Cases.Count))).ToArray()))
            .ToArray();
        return new(1, Disclaimer, sortedTests, features,
            diagnostics.OrderBy(d => d.Path, Order).ThenBy(d => d.Line).ThenBy(d => d.Code, Order)
                .ThenBy(d => d.Message, Order).ToArray());
    }

    /// <summary>
    /// Read only .cs files below a directory, excluding build outputs and version-control directories.
    /// Exceeding any byte, file-count or traversal-depth limit throws InvalidDataException.
    /// </summary>
    public static TraceabilityReport GenerateFromDirectory(
        string directory, IEnumerable<string>? implicitUsings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        var files = new List<TraceabilitySource>();
        var projectUsings = new Dictionary<string, string[]>(Order);
        var projectScopes = new Dictionary<string, string>(Order);
        var projectCount = 0;
        long totalBytes = 0;
        Walk(root, 0, [], "");
        return GenerateCore(files, implicitUsings, projectUsings, projectScopes);

        void Walk(string current, int depth, string[] inheritedUsings, string inheritedScope)
        {
            if (depth > MaximumDirectoryDepth)
            {
                throw new InvalidDataException(
                    $"Source directory depth exceeds {MaximumDirectoryDepth}: {Path.GetRelativePath(root, current)}");
            }

            var projectPaths = Directory.EnumerateFiles(current, "*.csproj")
                .Where(p => (File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0)
                .Order(Order).ToArray();
            if (projectPaths.Length > 1)
            {
                throw new InvalidDataException(
                    $"Multiple project files share source directory: {Path.GetRelativePath(root, current)}");
            }

            var scope = inheritedScope;
            var usings = inheritedUsings;
            if (projectPaths.Length == 1)
            {
                if (++projectCount > MaximumProjectFiles)
                {
                    throw new InvalidDataException($"Project file count exceeds {MaximumProjectFiles}.");
                }

                scope = Path.GetRelativePath(root, projectPaths[0]).Replace('\\', '/');
                usings = ReadProjectUsings(projectPaths[0]);
            }

            foreach (var path in Directory.EnumerateFiles(current, "*.cs").Order(Order))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if (files.Count >= MaximumSourceFiles)
                {
                    throw new InvalidDataException($"Source file count exceeds {MaximumSourceFiles}.");
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > MaximumSourceFileBytes)
                {
                    throw new InvalidDataException(
                        $"Source file exceeds {MaximumSourceFileBytes} bytes: {Path.GetRelativePath(root, path)}");
                }

                if (totalBytes + stream.Length > MaximumTotalSourceBytes)
                {
                    throw new InvalidDataException($"Total source bytes exceed {MaximumTotalSourceBytes}.");
                }

                using var content = new MemoryStream();
                var buffer = new byte[8192];
                int count;
                while ((count = stream.Read(buffer)) != 0)
                {
                    if (content.Length + count > MaximumSourceFileBytes)
                    {
                        throw new InvalidDataException(
                            $"Source file exceeds {MaximumSourceFileBytes} bytes: {Path.GetRelativePath(root, path)}");
                    }

                    if (totalBytes + content.Length + count > MaximumTotalSourceBytes)
                    {
                        throw new InvalidDataException($"Total source bytes exceed {MaximumTotalSourceBytes}.");
                    }

                    content.Write(buffer, 0, count);
                }

                totalBytes += content.Length;
                content.Position = 0;
                using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                files.Add(new(relative, reader.ReadToEnd()));
                projectUsings.Add(relative, usings);
                projectScopes.Add(relative, scope);
            }

            foreach (var child in Directory.EnumerateDirectories(current).Order(Order))
            {
                if (new[] { "bin", "obj", ".git", ".vs", "node_modules" }
                        .Contains(Path.GetFileName(child), Order) ||
                    (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                Walk(child, depth + 1, usings, scope);
            }
        }
    }

    private static string[] ReadProjectUsings(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumProjectFileBytes)
            throw new InvalidDataException($"Project file exceeds {MaximumProjectFileBytes} bytes: {path}");

        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            if (content.Length + count > MaximumProjectFileBytes)
                throw new InvalidDataException($"Project file exceeds {MaximumProjectFileBytes} bytes: {path}");
            content.Write(buffer, 0, count);
        }

        content.Position = 0;
        try
        {
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaximumProjectFileBytes
            });
            var document = XDocument.Load(reader);
            var usings = new List<string>();
            foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "Using"))
            {
                if (element.Attributes().Any(a => a.Name.LocalName is "Condition" or "Alias" or "Static" or "Remove" or "Update") ||
                    element.Ancestors().Any(e => e.Attribute("Condition") != null))
                {
                    throw new InvalidDataException($"Conditional or modified project Using cannot be evaluated: {path}");
                }

                var name = (string?)element.Attribute("Include");
                if (name == null || name.Length == 0 ||
                    !name.Split('.').All(segment => segment.Length > 0 &&
                        (char.IsLetter(segment[0]) || segment[0] == '_') &&
                        segment.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_')))
                {
                    throw new InvalidDataException($"Project Using cannot be resolved as a namespace: {path}");
                }

                usings.Add(name);
            }

            return usings.Distinct(Order).ToArray();
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"Invalid project XML: {path}", exception);
        }
    }

    private static void ProcessMethod(MethodDeclarationSyntax method, UsingDirectiveSyntax[] imports,
        string[] injectedImports,
        string path, List<TraceabilityTest> tests, List<TraceabilityDiagnostic> diagnostics)
    {
        var attributes = method.AttributeLists.SelectMany(l => l.Attributes).ToArray();
        var framework = attributes.Select(a => TestFramework(a, imports, injectedImports))
            .FirstOrDefault(f => f != null);
        var unresolvedFramework = false;
        if (framework == null)
        {
            framework = attributes.Select(CandidateTestFramework).FirstOrDefault(f => f != null);
            unresolvedFramework = framework != null;
        }

        if (framework == null)
        {
            return;
        }

        var line = method.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var id = MethodId(method);
        if (unresolvedFramework)
        {
            diagnostics.Add(new("TRACE010", path, line,
                $"Test-like attribute on '{id}' cannot be resolved to a framework from source/project usings."));
        }

        var features = new List<string>();
        var requirements = new List<string>();
        foreach (var attribute in attributes)
        {
            var kind = MetadataKind(attribute, imports, injectedImports);
            if (kind == null)
            {
                continue;
            }

            var args = attribute.ArgumentList?.Arguments.Select(a => a.Expression).ToArray() ?? [];
            ExpressionSyntax? expression;
            if (kind == "Native")
            {
                if (args.Length != 2 || !TryString(args[0], out var key) ||
                    key is not ("Feature" or "Requirement"))
                {
                    // A native property with a different name does not participate in traceability.
                    if (args.Length > 0 && TryString(args[0], out _))
                    {
                        continue;
                    }

                    diagnostics.Add(new("TRACE006", path, line,
                        $"Cannot resolve metadata key on '{attribute.Name}' from source literals."));
                    continue;
                }

                kind = key;
                expression = args[1];
            }
            else
            {
                expression = args.Length == 1 ? args[0] : null;
            }

            if (expression == null || !TryString(expression, out var value))
            {
                diagnostics.Add(new("TRACE006", path, line,
                    $"Cannot resolve {kind} value on '{attribute.Name}' from source literals."));
                continue;
            }

            if (kind == "Feature") features.Add(value);
            else requirements.Add(value);
        }

        if (features.Count == 0)
        {
            diagnostics.Add(new("TRACE002", path, line, $"Test '{id}' has no method-level Feature."));
        }
        else if (features.Count > 1)
        {
            diagnostics.Add(new("TRACE003", path, line, $"Test '{id}' has duplicate or conflicting Feature annotations."));
        }

        var feature = features.Count == 1 ? features[0] : null;
        if (feature != null && !ValidId(feature, "FEAT-"))
        {
            diagnostics.Add(new("TRACE004", path, line, $"Invalid Feature identifier '{feature}'."));
        }

        var seen = new HashSet<string>(Order);
        foreach (var requirement in requirements)
        {
            if (!seen.Add(requirement))
            {
                diagnostics.Add(new("TRACE003", path, line, $"Duplicate Requirement '{requirement}' on '{id}'."));
            }

            if (!ValidId(requirement, "FEAT-") || requirement.IndexOf('-', 5) < 6)
            {
                diagnostics.Add(new("TRACE004", path, line, $"Invalid Requirement identifier '{requirement}'."));
            }
            else if (feature != null && !requirement.StartsWith(feature + "-", StringComparison.Ordinal))
            {
                diagnostics.Add(new("TRACE005", path, line,
                    $"Requirement '{requirement}' does not belong to Feature '{feature}'."));
            }
        }

        var cases = BuildCases(attributes, imports, injectedImports, id, path, line, diagnostics);
        tests.Add(new(id, framework, path, line, feature,
            seen.Order(Order).ToArray(), cases));
    }

    private static IReadOnlyList<TraceabilityCase> BuildCases(
        AttributeSyntax[] attributes, UsingDirectiveSyntax[] imports, string[] injectedImports,
        string id, string path, int line,
        List<TraceabilityDiagnostic> diagnostics)
    {
        var cases = new List<TraceabilityCase>();
        var rows = new HashSet<string>(Order);
        foreach (var attribute in attributes)
        {
            var name = ResolvedName(attribute.Name, imports);
            var shortName = ShortName(name);
            var inline = shortName is "InlineData" or "DataRow" or "TestCase";
            var dynamic = shortName is "MemberData" or "ClassData" or "DynamicData" or "TestCaseSource";
            if (!inline && !dynamic)
            {
                continue;
            }

            var expected = shortName switch
            {
                "InlineData" or "MemberData" or "ClassData" => "Xunit",
                "DataRow" or "DynamicData" => "Microsoft.VisualStudio.TestTools.UnitTesting",
                _ => "NUnit.Framework"
            };
            if (!InNamespace(name, expected, imports, injectedImports))
            {
                continue;
            }

            var args = attribute.ArgumentList?.Arguments
                .Select(a => a.Expression.WithoutTrivia().NormalizeWhitespace().ToFullString()).ToArray() ?? [];
            if (inline && !rows.Add(shortName + ":" + string.Join("\u001f", args)))
            {
                diagnostics.Add(new("TRACE009", path, line, $"Duplicate inline data case on '{id}'."));
            }

            cases.Add(new(id + "#" + (cases.Count + 1).ToString("D4",
                    System.Globalization.CultureInfo.InvariantCulture),
                dynamic ? "DynamicSource" : "Inline", shortName, args));
        }

        if (cases.Count == 0)
        {
            var isParameterized = attributes.Any(a => ShortName(ResolvedName(a.Name, imports))
                is "Theory" or "DataTestMethod");
            cases.Add(new(id + "#0001", isParameterized ? "Undetermined" : "Method",
                "", Array.Empty<string>()));
        }

        return cases;
    }

    private static string? TestFramework(AttributeSyntax attribute, UsingDirectiveSyntax[] imports,
        string[] injectedImports)
    {
        var name = ResolvedName(attribute.Name, imports);
        var shortName = ShortName(name);
        if (shortName is "TestMethod" or "DataTestMethod" &&
            InNamespace(name, "Microsoft.VisualStudio.TestTools.UnitTesting", imports, injectedImports))
            return "MSTest";
        if (shortName is "Fact" or "Theory" &&
            InNamespace(name, "Xunit", imports, injectedImports))
            return "xUnit v2/v3";
        if (shortName is "Test" or "TestCase" or "TestCaseSource" or "Theory" &&
            InNamespace(name, "NUnit.Framework", imports, injectedImports))
            return "NUnit";
        return null;
    }

    private static string? CandidateTestFramework(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        if (name.Contains('.'))
            return null;
        return ShortName(name) switch
        {
            "Fact" => "xUnit v2/v3 (unresolved)",
            "TestMethod" or "DataTestMethod" => "MSTest (unresolved)",
            "Test" or "TestCase" or "TestCaseSource" => "NUnit (unresolved)",
            "Theory" => "xUnit/NUnit (unresolved)",
            _ => null
        };
    }

    private static string? MetadataKind(AttributeSyntax attribute, UsingDirectiveSyntax[] imports,
        string[] injectedImports)
    {
        var name = ResolvedName(attribute.Name, imports);
        var shortName = ShortName(name);
        if (shortName is "TestProperty" &&
            InNamespace(name, "Microsoft.VisualStudio.TestTools.UnitTesting", imports, injectedImports) ||
            shortName is "Trait" && InNamespace(name, "Xunit", imports, injectedImports) ||
            shortName is "Property" && InNamespace(name, "NUnit.Framework", imports, injectedImports))
            return "Native";
        if (shortName is not ("Feature" or "Requirement"))
            return null;
        foreach (var adapter in new[] { "XunitV2", "XunitV3", "NUnit" })
        {
            if (InNamespace(name, "Mauve.Governance.Testing." + adapter, imports, injectedImports))
                return shortName;
        }

        return null;
    }

    private static string ResolvedName(NameSyntax name, UsingDirectiveSyntax[] imports)
    {
        var text = name.ToString().Replace("global::", "", StringComparison.Ordinal);
        var first = text.Split('.')[0];
        var alias = imports.FirstOrDefault(u => u.Alias?.Name.Identifier.ValueText == first);
        if (alias != null)
            return alias.Name + text[first.Length..];
        return text.EndsWith("Attribute", StringComparison.Ordinal) ? text[..^9] : text;
    }

    private static string ShortName(string name)
    {
        var value = name.Split('.').Last();
        return value.EndsWith("Attribute", StringComparison.Ordinal) ? value[..^9] : value;
    }

    private static bool InNamespace(string name, string ns, UsingDirectiveSyntax[] imports,
        string[] injectedImports)
    {
        if (name.StartsWith(ns + ".", StringComparison.Ordinal) &&
            !name[(ns.Length + 1)..].Contains('.'))
            return true;
        if (name.Contains('.'))
            return false;
        return injectedImports.Contains(ns, Order) ||
            imports.Any(u => u.Alias == null &&
                u.Name?.ToString().Replace("global::", "", StringComparison.Ordinal) == ns);
    }

    private static bool TryString(ExpressionSyntax expression, out string value)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        if (expression is MemberAccessExpressionSyntax member &&
            member.Name.Identifier.ValueText is "Feature" or "Requirement" &&
            (member.Expression.ToString().EndsWith("TestMetadata", StringComparison.Ordinal) ||
             member.Expression.ToString().EndsWith("Metadata", StringComparison.Ordinal)))
        {
            value = member.Name.Identifier.ValueText;
            return true;
        }

        value = "";
        return false;
    }

    private static string MethodId(MethodDeclarationSyntax method)
    {
        var namespaces = method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse().Select(n => n.Name.ToString());
        var types = method.Ancestors().OfType<TypeDeclarationSyntax>()
            .Reverse().Select(t => t.Identifier.ValueText +
                Arity(t.TypeParameterList?.Parameters.Count ?? 0));
        var prefix = string.Join(".", namespaces.Concat(types));
        var parameters = string.Join(",", method.ParameterList.Parameters
            .Select(p => p.Type?.NormalizeWhitespace().ToFullString() ?? "?"));
        return (prefix.Length == 0 ? "" : prefix + ".") + method.Identifier.ValueText +
            Arity(method.TypeParameterList?.Parameters.Count ?? 0) + "(" + parameters + ")";
    }

    private static string Arity(int count) => count == 0 ? "" :
        "`" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static bool ValidId(string value, string prefix)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var suffix = value.AsSpan(prefix.Length);
        if (suffix.Length == 0 || !Alphanumeric(suffix[0]) || !Alphanumeric(suffix[^1]))
            return false;
        return suffix.ToArray().All(c => Alphanumeric(c) || c is '-' or '_' or '.');
    }

    private static bool Alphanumeric(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
