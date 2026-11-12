using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace Mauve.Governance.TestResults;

public sealed record TestObservation(
    string Id,
    string Name,
    string Feature,
    string[] Requirements,
    string Outcome);

public sealed record ImportedRun(string Sha256, string RunId, TestObservation[] Tests);

public sealed record Verdict(
    string Id,
    string Status,
    int Passed,
    int Failed,
    int Other,
    int Skipped,
    int NotExecuted);

public sealed record EvidenceReport(
    int SchemaVersion,
    ImportedRun[] Runs,
    Verdict[] Features,
    Verdict[] Requirements,
    Verdict[] Tests);

[JsonSerializable(typeof(EvidenceReport))]
internal sealed partial class EvidenceJsonContext : JsonSerializerContext;

public static class TrxEvidence
{
    private const int MaximumRecognizedMetadataKeyLength = 11;
    private const int MaximumRequirementIdLength = 262;
    private const int ValueChunkSize = 4096;

    public static EvidenceReport Empty { get; } = Build([]);

    public static EvidenceReport LoadReport(string path)
    {
        using var stream = File.OpenRead(path);
        var report = JsonSerializer.Deserialize(stream, EvidenceJsonContext.Default.EvidenceReport)
            ?? throw new InvalidDataException("Governance report is empty.");
        if (report.SchemaVersion != 1 || report.Runs is null ||
            report.Runs.Any(static run => run is null || run.Sha256 is not { Length: 64 } ||
                run.RunId is null || run.Tests is null ||
                run.Tests.Any(static test => test is null || test.Id is null ||
                    test.Name is null || test.Feature is null || test.Outcome is null ||
                    test.Requirements is null)))
        {
            throw new InvalidDataException("Governance report schema is invalid.");
        }

        var rebuilt = Build(report.Runs);
        if (JsonSerializer.Serialize(rebuilt, EvidenceJsonContext.Default.EvidenceReport) !=
            JsonSerializer.Serialize(report, EvidenceJsonContext.Default.EvidenceReport))
        {
            throw new InvalidDataException("Governance report observations or aggregates are inconsistent.");
        }

        return rebuilt;
    }

    public static EvidenceReport AddTrx(EvidenceReport existing, string path)
    {
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (existing.Runs.Any(run => run.Sha256 == hash))
        {
            return existing;
        }

        stream.Position = 0;
        var run = Parse(stream, hash);
        if (existing.Runs.Any(previous => previous.RunId == run.RunId))
        {
            throw new InvalidDataException("TRX run id was already imported with different content.");
        }

        return Build([..existing.Runs, run]);
    }

    public static void Save(EvidenceReport report, string path)
    {
        var destination = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".governance-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, report, EvidenceJsonContext.Default.EvidenceReport);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static ImportedRun Parse(Stream stream, string hash)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true
        });
        var definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
        var results = new List<Result>();
        var nestedResults = new Stack<(int Index, int Depth)>();
        var innerResults = new Stack<int>();
        string? runId = null;
        var definitionsDepth = -1;
        var resultsDepth = -1;
        var sawDefinitions = false;
        var sawResults = false;

        while (reader.Read())
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                ConsumeValue(reader);
                continue;
            }

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (resultsDepth >= 0 && reader.Depth == resultsDepth &&
                    reader.LocalName == "Results")
                {
                    resultsDepth = -1;
                }

                if (definitionsDepth >= 0 && reader.Depth == definitionsDepth &&
                    reader.LocalName == "TestDefinitions")
                {
                    definitionsDepth = -1;
                }

                if (resultsDepth >= 0 && reader.LocalName == "UnitTestResult" &&
                    nestedResults.TryPeek(out var openResult) && openResult.Depth == reader.Depth)
                {
                    nestedResults.Pop();
                }

                if (resultsDepth >= 0 && reader.LocalName == "InnerResults" &&
                    innerResults.TryPeek(out var innerDepth) && innerDepth == reader.Depth)
                {
                    innerResults.Pop();
                }

                continue;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Depth == 0)
            {
                if (reader.LocalName != "TestRun" || runId is not null)
                {
                    throw new InvalidDataException("TRX root must be a TestRun element.");
                }

                runId = Required(reader.GetAttribute("id"), "TRX run id");
                continue;
            }

            if (reader.Depth == 1)
            {
                if (reader.LocalName == "TestDefinitions")
                {
                    if (sawDefinitions)
                    {
                        throw new InvalidDataException("TRX contains duplicate test definitions.");
                    }

                    sawDefinitions = true;
                    definitionsDepth = reader.IsEmptyElement ? -1 : reader.Depth;
                }
                else if (reader.LocalName == "Results")
                {
                    if (sawResults)
                    {
                        throw new InvalidDataException("TRX contains duplicate result sections.");
                    }

                    sawResults = true;
                    resultsDepth = reader.IsEmptyElement ? -1 : reader.Depth;
                }

                continue;
            }

            if (definitionsDepth >= 0 && reader.Depth == definitionsDepth + 1 &&
                reader.LocalName == "UnitTest")
            {
                var id = Required(reader.GetAttribute("id"), "TRX test id");
                _ = Required(reader.GetAttribute("name"), "TRX test name");
                var metadata = ReadMetadata(reader);
                if (!definitions.TryAdd(id,
                        new Definition(metadata.Feature, metadata.Requirements)))
                {
                    throw new InvalidDataException("Duplicate TRX test definition.");
                }
            }
            else if (resultsDepth >= 0 && reader.LocalName == "InnerResults" &&
                nestedResults.TryPeek(out var enclosingResult) &&
                reader.Depth == enclosingResult.Depth + 1 && !reader.IsEmptyElement)
            {
                innerResults.Push(reader.Depth);
            }
            else if (resultsDepth >= 0 && reader.LocalName == "UnitTestResult" &&
                (reader.Depth == resultsDepth + 1 ||
                 (innerResults.TryPeek(out var innerDepth) && reader.Depth == innerDepth + 1)))
            {
                var nestedParent = nestedResults.TryPeek(out var parent)
                    ? parent.Index
                    : (int?)null;
                var result = new Result(
                    Required(reader.GetAttribute("testId"), "TRX result testId"),
                    Required(reader.GetAttribute("testName"), "TRX result testName"),
                    Required(reader.GetAttribute("outcome"), "TRX result outcome"),
                    Optional(reader.GetAttribute("executionId"), "TRX result executionId"),
                    Optional(reader.GetAttribute("parentExecutionId"), "TRX result parentExecutionId"),
                    reader.GetAttribute("resultType"),
                    nestedParent);
                var resultIndex = results.Count;
                results.Add(result);
                if (!reader.IsEmptyElement)
                {
                    nestedResults.Push((resultIndex, reader.Depth));
                }
            }
        }

        if (runId is null || !sawDefinitions || !sawResults || results.Count == 0)
        {
            throw new InvalidDataException(
                "TRX must have a TestRun id, test definitions, and at least one test result.");
        }

        var executionIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < results.Count; index++)
        {
            var executionId = results[index].ExecutionId;
            if (executionId is not null && !executionIds.TryAdd(executionId, index))
            {
                throw new InvalidDataException("TRX contains duplicate execution ids.");
            }

            if (!definitions.ContainsKey(results[index].TestId))
            {
                throw new InvalidDataException("TRX result has no matching test definition.");
            }
        }

        var parents = ResolveParents(results, executionIds);
        ValidateParentAcyclic(parents);
        var hasChildren = new bool[results.Count];
        var failedDescendant = new bool[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            if (parents[index] is not { } parentIndex)
            {
                continue;
            }

            hasChildren[parentIndex] = true;
            if (results[index].Outcome == "Failed")
            {
                var failedDefinition = definitions[results[index].TestId];
                var failedRequirements = failedDefinition.Requirements.ToHashSet(StringComparer.Ordinal);
                var ancestor = parentIndex;
                while (true)
                {
                    var ancestorDefinition = definitions[results[ancestor].TestId];
                    if (ancestorDefinition.Feature == failedDefinition.Feature &&
                        ancestorDefinition.Requirements.All(failedRequirements.Contains))
                    {
                        failedDescendant[ancestor] = true;
                    }

                    if (parents[ancestor] is not { } nextAncestor)
                    {
                        break;
                    }

                    ancestor = nextAncestor;
                }
            }
        }

        var observations = new List<TestObservation>(results.Count);
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            var hasInnerExecutions = hasChildren[index];
            var isAggregateParent = result.ResultType == "DataDrivenTest" || hasInnerExecutions;
            if (isAggregateParent && hasInnerExecutions &&
                (result.Outcome != "Failed" || failedDescendant[index]))
            {
                continue;
            }

            var definition = definitions[result.TestId];
            observations.Add(new TestObservation(
                result.TestId, result.Name, definition.Feature, definition.Requirements, result.Outcome));
        }

        return new ImportedRun(hash, runId, observations.ToArray());
    }

    private static int?[] ResolveParents(
        IReadOnlyList<Result> results,
        IReadOnlyDictionary<string, int> executionIds)
    {
        var parents = new int?[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            int? explicitParent = null;
            if (!string.IsNullOrWhiteSpace(result.ParentExecutionId))
            {
                if (!executionIds.TryGetValue(result.ParentExecutionId, out var parentIndex))
                {
                    throw new InvalidDataException("TRX result references an unknown parent execution.");
                }

                explicitParent = parentIndex;
            }

            if (result.NestedParentIndex is { } nestedParent &&
                explicitParent is { } declaredParent && nestedParent != declaredParent)
            {
                throw new InvalidDataException("TRX result has conflicting parent execution identities.");
            }

            parents[index] = explicitParent ?? result.NestedParentIndex;
            if (parents[index] == index)
            {
                throw new InvalidDataException("TRX result cannot be its own parent.");
            }
        }

        return parents;
    }

    private static void ValidateParentAcyclic(IReadOnlyList<int?> parents)
    {
        var state = new byte[parents.Count];
        var path = new List<int>();
        for (var index = 0; index < parents.Count; index++)
        {
            if (state[index] != 0)
            {
                continue;
            }

            path.Clear();
            var current = index;
            while (state[current] == 0)
            {
                state[current] = 1;
                path.Add(current);
                if (parents[current] is not { } parent)
                {
                    break;
                }

                current = parent;
            }

            if (state[current] == 1 && parents[current] is not null)
            {
                throw new InvalidDataException("TRX result parent relationships contain a cycle.");
            }

            foreach (var visited in path)
            {
                state[visited] = 2;
            }
        }
    }

    private static (string Feature, string[] Requirements) ReadMetadata(XmlReader definition)
    {
        var properties = new List<(string? Name, string? Value)>();
        var inProperties = false;
        using var subtree = definition.ReadSubtree();
        while (subtree.Read())
        {
            if (subtree.Depth == 1 && subtree.NodeType == XmlNodeType.EndElement)
            {
                inProperties = false;
            }
            else if (subtree.Depth == 1 && subtree.NodeType == XmlNodeType.Element)
            {
                inProperties = subtree.LocalName is "Properties" or "TestProperties" or "Traits" &&
                    !subtree.IsEmptyElement;
            }

            if (subtree.NodeType == XmlNodeType.Element &&
                subtree.LocalName is "Property" or "TestProperty" or "Trait" &&
                (subtree.Depth == 1 || (subtree.Depth == 2 && inProperties)))
            {
                if (ReadMetadataProperty(subtree) is { } property)
                {
                    properties.Add(property);
                }
            }
            else if (subtree.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                     XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                ConsumeValue(subtree);
            }
        }

        var features = properties.Where(static item => item.Name == "Feature")
            .Select(static item => item.Value).ToArray();
        if (features.Length != 1 || !IsFeature(features[0]))
        {
            throw new InvalidDataException("TRX test needs exactly one valid Feature property.");
        }

        var requirements = properties.Where(static item => item.Name == "Requirement")
            .Select(static item => item.Value)
            .ToArray();
        if (requirements.Any(item => item is null || !IsRequirement(item, features[0]!)) ||
            requirements.Distinct(StringComparer.Ordinal).Count() != requirements.Length)
        {
            throw new InvalidDataException("TRX test has invalid Requirement properties.");
        }

        return (features[0]!, requirements.Select(static item => item!)
            .Order(StringComparer.Ordinal).ToArray());
    }

    private static (string? Name, string? Value)? ReadMetadataProperty(XmlReader reader)
    {
        var hasName = TryReadAttribute(reader, "Name", MaximumRecognizedMetadataKeyLength, out var name) ||
            TryReadAttribute(reader, "name", MaximumRecognizedMetadataKeyLength, out name);
        if (hasName)
        {
            if (name is not ("Feature" or "Requirement"))
            {
                ConsumeSubtree(reader);
                return null;
            }

            var hasAttributeValue = TryReadAttribute(reader, "Value", MaximumRequirementIdLength, out var value) ||
                TryReadAttribute(reader, "value", MaximumRequirementIdLength, out value);
            if (hasAttributeValue)
            {
                ConsumeSubtree(reader);
                return (name, value);
            }

            ConsumeSubtree(reader);
            return (name, null);
        }

        using var propertyReader = reader.ReadSubtree();
        var key = (string?)null;
        string? childValue = null;
        var hasKey = false;
        var hasValue = false;
        while (propertyReader.Read())
        {
            if (propertyReader.NodeType != XmlNodeType.Element || propertyReader.Depth != 1)
            {
                if (propertyReader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                    XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
                {
                    ConsumeValue(propertyReader);
                }

                continue;
            }

            if (propertyReader.LocalName == "Key")
            {
                if (hasKey)
                {
                    throw new InvalidDataException("Duplicate TRX test property key.");
                }

                hasKey = true;
                key = ReadElementValue(propertyReader, MaximumRecognizedMetadataKeyLength);
            }
            else if (propertyReader.LocalName == "Value")
            {
                if (hasValue)
                {
                    throw new InvalidDataException("Duplicate TRX test property value.");
                }

                hasValue = true;
                childValue = key is "Feature" or "Requirement"
                    ? ReadElementValue(propertyReader, MaximumRequirementIdLength)
                    : ConsumeElement(propertyReader);
            }
        }

        return key is "Feature" or "Requirement" ? (key, childValue) : null;
    }

    private static string? ReadElementValue(XmlReader reader, int maximumLength)
    {
        if (reader.IsEmptyElement)
        {
            return string.Empty;
        }

        var depth = reader.Depth;
        var value = new StringBuilder(Math.Min(maximumLength, 64));
        var buffer = ArrayPool<char>.Shared.Rent(ValueChunkSize);
        var tooLong = false;
        try
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                {
                    return tooLong ? null : value.ToString();
                }

                if (reader.NodeType is not (XmlNodeType.Text or XmlNodeType.CDATA or
                    XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace))
                {
                    continue;
                }

                while (true)
                {
                    var count = reader.ReadValueChunk(buffer, 0, ValueChunkSize);
                    if (count == 0)
                    {
                        break;
                    }

                    if (!tooLong && value.Length + count <= maximumLength)
                    {
                        value.Append(buffer, 0, count);
                    }
                    else
                    {
                        tooLong = true;
                    }
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }

        return tooLong ? null : value.ToString();
    }

    private static string? ConsumeElement(XmlReader reader)
    {
        ConsumeSubtree(reader);
        return null;
    }

    private static void ConsumeSubtree(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return;
        }

        var elementDepth = reader.Depth;
        while (reader.Read())
        {
            if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or
                XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
            {
                ConsumeValue(reader);
            }

            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == elementDepth)
            {
                return;
            }
        }
    }

    private static bool TryReadAttribute(
        XmlReader reader,
        string name,
        int maximumLength,
        out string? value)
    {
        if (!reader.MoveToAttribute(name))
        {
            value = null;
            return false;
        }

        try
        {
            var builder = new StringBuilder(Math.Min(maximumLength, 64));
            var buffer = ArrayPool<char>.Shared.Rent(ValueChunkSize);
            var tooLong = false;
            try
            {
                while (true)
                {
                    var count = reader.ReadValueChunk(buffer, 0, ValueChunkSize);
                    if (count == 0)
                    {
                        break;
                    }

                    if (!tooLong && builder.Length + count <= maximumLength)
                    {
                        builder.Append(buffer, 0, count);
                    }
                    else
                    {
                        tooLong = true;
                    }
                }
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }

            value = tooLong ? null : builder.ToString();
        }
        finally
        {
            reader.MoveToElement();
        }

        return true;
    }

    private static void ConsumeValue(XmlReader reader)
    {
        var buffer = ArrayPool<char>.Shared.Rent(ValueChunkSize);
        try
        {
            while (reader.ReadValueChunk(buffer, 0, ValueChunkSize) != 0)
            {
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static bool IsFeature(string? value) =>
        value is not null && value.StartsWith("FEAT-", StringComparison.Ordinal) &&
        ValidIdSuffix(value.AsSpan(5));

    private static bool IsRequirement(string? value, string feature) =>
        value is not null && value.StartsWith(feature + "-", StringComparison.Ordinal) &&
        ValidIdSuffix(value.AsSpan(feature.Length + 1));

    private static bool ValidIdSuffix(ReadOnlySpan<char> suffix)
    {
        if (suffix.Length is < 1 or > 128 ||
            !AsciiAlphanumeric(suffix[0]) || !AsciiAlphanumeric(suffix[^1]))
        {
            return false;
        }

        foreach (var character in suffix)
        {
            if (!AsciiAlphanumeric(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AsciiAlphanumeric(char value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string Required(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidDataException($"Missing {field}.");

    private static string? Optional(string? value, string field) =>
        value is null ? null : Required(value, field);

    private static EvidenceReport Build(ImportedRun[] runs)
    {
        var sorted = runs.OrderBy(static run => run.Sha256, StringComparer.Ordinal).ToArray();
        if (sorted.Select(static run => run.Sha256).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
        {
            throw new InvalidDataException("Governance report contains duplicate TRX imports.");
        }
        if (sorted.Select(static run => run.RunId).Distinct(StringComparer.Ordinal).Count() != sorted.Length)
        {
            throw new InvalidDataException("Governance report contains duplicate run ids.");
        }

        var features = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var requirements = new Dictionary<string, Counter>(StringComparer.Ordinal);
        var tests = new Dictionary<string, Counter>(StringComparer.Ordinal);
        foreach (var run in sorted)
        {
            if (run.Sha256.Length != 64 || !run.Sha256.All(Uri.IsHexDigit) ||
                run.Sha256 != run.Sha256.ToUpperInvariant() ||
                string.IsNullOrWhiteSpace(run.RunId) || run.Tests is null)
            {
                throw new InvalidDataException("Governance report has an invalid run.");
            }

            foreach (var test in run.Tests)
            {
                if (!IsFeature(test.Feature) ||
                    test.Requirements is null ||
                    test.Requirements.Any(requirement => !IsRequirement(requirement, test.Feature)) ||
                    test.Requirements.Distinct(StringComparer.Ordinal).Count() != test.Requirements.Length ||
                    string.IsNullOrWhiteSpace(test.Id) ||
                    string.IsNullOrWhiteSpace(test.Name) ||
                    string.IsNullOrWhiteSpace(test.Outcome))
                {
                    throw new InvalidDataException("Governance report has an invalid test observation.");
                }

                Add(features, test.Feature, test.Outcome);
                foreach (var requirement in test.Requirements)
                {
                    Add(requirements, requirement, test.Outcome);
                }

                Add(tests, test.Feature + "/" + test.Id, test.Outcome);
            }
        }

        return new EvidenceReport(1, sorted,
            Verdicts(features), Verdicts(requirements), Verdicts(tests));
    }

    private static void Add(Dictionary<string, Counter> counters, string key, string outcome)
    {
        if (!counters.TryGetValue(key, out var counter))
        {
            counter = new Counter();
            counters.Add(key, counter);
        }

        switch (outcome)
        {
            case "Passed": counter.Passed++; break;
            case "Failed": counter.Failed++; break;
            case "Skipped": counter.Skipped++; break;
            case "NotExecuted": counter.NotExecuted++; break;
            default: counter.Other++; break;
        }
    }

    private static Verdict[] Verdicts(Dictionary<string, Counter> counters) =>
        counters.OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item =>
            {
                var c = item.Value;
                var status = c.Passed > 0 && c.Failed > 0 ? "Mixed"
                    : c.Failed > 0 ? "Failed"
                    : c.Other > 0 ? "Indeterminate"
                    : c.Passed > 0 ? "Passed" : "NotObserved";
                return new Verdict(item.Key, status, c.Passed, c.Failed, c.Other,
                    c.Skipped, c.NotExecuted);
            }).ToArray();

    private sealed class Counter
    {
        public int Passed;
        public int Failed;
        public int Other;
        public int Skipped;
        public int NotExecuted;
    }

    private sealed record Definition(string Feature, string[] Requirements);

    private sealed record Result(
        string TestId,
        string Name,
        string Outcome,
        string? ExecutionId,
        string? ParentExecutionId,
        string? ResultType,
        int? NestedParentIndex);
}
