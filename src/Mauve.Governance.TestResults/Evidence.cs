using System.Security.Cryptography;
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
    private const long MaximumTrxBytes = 32 * 1024 * 1024;
    private const int MaximumResults = 100_000;
    private const int MaximumRuns = 1_000;
    private const int MaximumReportBytes = 64 * 1024 * 1024;

    public static EvidenceReport Empty { get; } = Build([]);

    public static EvidenceReport LoadReport(string path)
    {
        using var stream = OpenBounded(path, MaximumReportBytes);
        var report = JsonSerializer.Deserialize(stream, EvidenceJsonContext.Default.EvidenceReport)
            ?? throw new InvalidDataException("Governance report is empty.");
        if (report.SchemaVersion != 1 || report.Runs is null ||
            report.Runs.Length > MaximumRuns ||
            report.Runs.Any(static run => run is null || run.Sha256 is not { Length: 64 } ||
                run.RunId is null || run.Tests is null ||
                run.Tests.Length > MaximumResults ||
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
        using var stream = OpenBounded(path, MaximumTrxBytes);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (existing.Runs.Any(run => run.Sha256 == hash))
        {
            return existing;
        }

        if (existing.Runs.Length >= MaximumRuns)
        {
            throw new InvalidDataException("Governance report exceeds the run limit.");
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

    private static FileStream OpenBounded(string path, long maximum)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= maximum)
        {
            return stream;
        }

        stream.Dispose();
        throw new InvalidDataException($"Input exceeds the {maximum}-byte limit.");
    }

    private static ImportedRun Parse(Stream stream, string hash)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumTrxBytes,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true
        });
        var definitions = new Dictionary<string, (string Name, string Feature, string[] Requirements)>(
            StringComparer.Ordinal);
        var results = new List<(string Id, string Name, string Outcome)>();
        string? runId = null;
        var inDefinitions = false;
        var inResults = false;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 1)
            {
                inDefinitions = false;
                inResults = false;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Depth == 0 && reader.LocalName == "TestRun")
            {
                runId = Required(reader.GetAttribute("id"), "TRX run id");
                continue;
            }

            if (reader.Depth == 1)
            {
                inDefinitions = reader.LocalName == "TestDefinitions";
                inResults = reader.LocalName == "Results";
                continue;
            }

            if (inDefinitions && reader.Depth == 2 && reader.LocalName == "UnitTest" &&
                reader.GetAttribute("id") is { } id)
            {
                if (definitions.Count >= MaximumResults)
                {
                    throw new InvalidDataException("TRX exceeds the test-definition limit.");
                }

                id = Required(id, "TRX test id");
                var name = Required(reader.GetAttribute("name"), "TRX test name");
                using var subtree = reader.ReadSubtree();
                var metadata = ReadMetadata(subtree);
                if (!definitions.TryAdd(id, (name, metadata.Feature, metadata.Requirements)))
                {
                    throw new InvalidDataException("Duplicate TRX test definition.");
                }
            }
            else if (inResults && reader.Depth == 2 && reader.LocalName == "UnitTestResult")
            {
                if (results.Count >= MaximumResults)
                {
                    throw new InvalidDataException("TRX exceeds the test-result limit.");
                }

                results.Add((
                    Required(reader.GetAttribute("testId"), "TRX result testId"),
                    Required(reader.GetAttribute("testName"), "TRX result testName"),
                    Required(reader.GetAttribute("outcome"), "TRX result outcome")));
            }
        }

        if (runId is null || results.Count == 0)
        {
            throw new InvalidDataException("TRX must have a TestRun id and at least one test result.");
        }

        var observations = results.Select(result =>
        {
            if (!definitions.TryGetValue(result.Id, out var definition) ||
                definition.Name != result.Name)
            {
                throw new InvalidDataException("TRX result has no matching test definition.");
            }

            return new TestObservation(
                result.Id, result.Name, definition.Feature, definition.Requirements, result.Outcome);
        }).ToArray();
        return new ImportedRun(hash, runId, observations);
    }

    private static (string Feature, string[] Requirements) ReadMetadata(XmlReader subtree)
    {
        var properties = new List<(string? Name, string? Value)>();
        while (subtree.Read())
        {
            if (subtree.Depth > 64)
            {
                throw new InvalidDataException("TRX test definition exceeds the XML nesting limit.");
            }

            if (subtree.NodeType != XmlNodeType.Element ||
                subtree.LocalName is not ("Property" or "TestProperty" or "Trait"))
            {
                continue;
            }

            if (properties.Count >= 64)
            {
                throw new InvalidDataException("TRX test definition exceeds the metadata limit.");
            }

            var name = subtree.GetAttribute("Name") ?? subtree.GetAttribute("name");
            var value = subtree.GetAttribute("Value") ?? subtree.GetAttribute("value");
            if (subtree.LocalName == "Property" && name is null && value is null)
            {
                using var property = subtree.ReadSubtree();
                if (property.Read())
                {
                    while (property.Read())
                    {
                        if (property.Depth > 64)
                        {
                            throw new InvalidDataException("TRX test property exceeds the XML nesting limit.");
                        }
                        if (property.Depth != 1 || property.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }
                        if (property.LocalName == "Key")
                        {
                            if (name is not null)
                            {
                                throw new InvalidDataException("Duplicate TRX test property key.");
                            }
                            name = property.ReadElementContentAsString();
                        }
                        if (property.NodeType == XmlNodeType.Element &&
                            property.Depth == 1 && property.LocalName == "Value")
                        {
                            if (value is not null)
                            {
                                throw new InvalidDataException("Duplicate TRX test property value.");
                            }
                            value = property.ReadElementContentAsString();
                        }
                    }
                }
            }
            if (name?.Length > 4096 || value?.Length > 4096)
            {
                throw new InvalidDataException("TRX test property exceeds the metadata size limit.");
            }
            properties.Add((name, value));
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

        return (features[0]!, requirements.Select(static item => item!).Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsFeature(string? value) =>
        value is not null && value.StartsWith("FEAT-", StringComparison.Ordinal) &&
        ValidIdSuffix(value.AsSpan(5));

    private static bool IsRequirement(string value, string feature) =>
        value.StartsWith(feature + "-", StringComparison.Ordinal) &&
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
        !string.IsNullOrWhiteSpace(value) && value.Length <= 4096
            ? value
            : throw new InvalidDataException($"Missing or oversized {field}.");

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
                string.IsNullOrWhiteSpace(run.RunId) || run.Tests.Length > MaximumResults)
            {
                throw new InvalidDataException("Governance report has an invalid run.");
            }

            foreach (var test in run.Tests)
            {
                if (!IsFeature(test.Feature) ||
                    test.Requirements.Any(requirement => !IsRequirement(requirement, test.Feature)) ||
                    test.Requirements.Distinct(StringComparer.Ordinal).Count() != test.Requirements.Length ||
                    string.IsNullOrWhiteSpace(test.Id) || test.Id.Length > 4096 ||
                    string.IsNullOrWhiteSpace(test.Name) || test.Name.Length > 4096 ||
                    string.IsNullOrWhiteSpace(test.Outcome) || test.Outcome.Length > 4096)
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
}
