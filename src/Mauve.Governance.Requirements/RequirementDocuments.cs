using Markdig;
using Markdig.Syntax;
using System.Text;

namespace Mauve.Governance.Requirements;

public static class RequirementDocuments
{
    private const int MaxDocumentBytes = 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly string[] ActiveSections =
    [
        "Status", "Capability", "Context", "Requirements",
        "Metrics Impact", "Verification", "Diagrams"
    ];

    private static readonly string[] ExtensionSections = ["Design", "Implementation Plan"];

    public static void Validate(string root, bool strictPlanned = false)
        => ValidateCore(root, strictPlanned, new Counts());

    public static IReadOnlySet<string> GetActiveRequirementIds(string root)
        => ValidateCore(root, strictPlanned: false, new Counts());

    public static RequirementSummary Summarize(string root, bool strictPlanned = false)
    {
        var counts = new Counts();
        ValidateCore(root, strictPlanned, counts);
        return new RequirementSummary(counts.ActiveDocuments, counts.ActiveRequirements,
            counts.PlannedDocuments, counts.PlannedRequirements);
    }

    private static readonly string[] MetricLabels =
        ["Instruments", "Emission points", "Tags", "Privacy", "Dashboards", "Tests"];

    private sealed class Counts
    {
        public readonly HashSet<string> Declared = new(StringComparer.Ordinal);
        public int ActiveDocuments;
        public int ActiveRequirements;
        public int PlannedDocuments;
        public int PlannedRequirements;
    }

    private static IReadOnlySet<string> ValidateCore(string root, bool strictPlanned, Counts counts)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!Directory.Exists(root) || IsLink(root))
        {
            throw new InvalidDataException("Repository root must be an existing, non-linked directory.");
        }

        var docs = Path.Combine(root, "docs");
        if (Directory.Exists(docs) && IsLink(docs))
        {
            throw new InvalidDataException("Feature document parent directory must not be a symlink.");
        }

        var requirementsRoot = Path.Combine(root, "docs", "Requirements");
        if (!Directory.Exists(requirementsRoot))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var plannedRoot = Path.Combine(requirementsRoot, "Planned");
        if (IsLink(requirementsRoot) || Directory.Exists(plannedRoot) && IsLink(plannedRoot))
        {
            throw new InvalidDataException("Feature document directories must not be symlinks.");
        }

        var activeReadme = Path.Combine(requirementsRoot, "README.md");
        var plannedReadme = Path.Combine(plannedRoot, "README.md");
        if (!File.Exists(activeReadme) || !Directory.Exists(plannedRoot) ||
            !File.Exists(plannedReadme))
        {
            throw new InvalidDataException("Feature lifecycle requires README files in docs/Requirements and its Planned directory.");
        }

        if (IsLink(activeReadme) || IsLink(plannedReadme))
        {
            throw new InvalidDataException("Feature lifecycle README files must not be symlinks.");
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        CheckFiles(requirementsRoot, planned: false, strictPlanned, counts, claimed, active);
        CheckFiles(plannedRoot, planned: true, strictPlanned, counts, claimed, active);
        return active;
    }

    internal static string ReadBounded(Stream stream)
    {
        if (stream.Length > MaxDocumentBytes)
        {
            throw new InvalidDataException("Feature document exceeds the 1 MiB limit.");
        }

        var bytes = new byte[MaxDocumentBytes + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = stream.Read(bytes.AsSpan(count));
            if (read == 0)
            {
                break;
            }

            count += read;
        }

        if (count > MaxDocumentBytes)
        {
            throw new InvalidDataException("Feature document exceeds the 1 MiB limit.");
        }

        var offset = count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        try
        {
            return Utf8.GetString(bytes.AsSpan(offset, count - offset));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Feature document must be valid UTF-8.", exception);
        }
    }

    private static void CheckFiles(string directory, bool planned, bool strictPlanned, Counts counts,
        HashSet<string> claimed, HashSet<string> active)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "FEAT-*.md").Order(StringComparer.Ordinal))
        {
            if (IsLink(path))
            {
                throw new InvalidDataException("Feature documents must not be symlinks.");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var source = ReadBounded(stream);
            var first = source.Split('\n', 2)[0].TrimEnd('\r');
            if (!first.StartsWith("# FEAT-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Feature document must begin with an owning FEAT heading.");
            }

            var feature = first[2..].Split([':', ' '], 2)[0];
            if (feature.Length <= 5 || !ValidSuffix(feature.AsSpan(5)) ||
                !Path.GetFileName(path).StartsWith(feature + "-", StringComparison.Ordinal))
            {
                throw new InvalidDataException("Feature heading must match its document filename.");
            }

            var blocks = Markdown.Parse(source).ToArray();
            if (blocks.Length == 0 || blocks[0] is not HeadingBlock { Level: 1 } firstHeading ||
                firstHeading.Span.Start != 0)
            {
                throw new InvalidDataException("Feature document must begin with an owning FEAT heading.");
            }

            var sections = blocks.OfType<HeadingBlock>().Where(static heading => heading.Level == 2)
                .Select(heading => HeadingText(source, heading)).ToArray();
            var structural = !planned || strictPlanned;
            if (structural && !HasValidSections(sections))
            {
                throw new InvalidDataException(planned
                    ? "Strict planned feature sections are missing or out of order."
                    : "Active feature sections are missing or out of order.");
            }

            if (planned)
            {
                counts.PlannedDocuments++;
            }
            else
            {
                counts.ActiveDocuments++;
            }

            CheckDeclarations(source, blocks, structural, counts.Declared);

            if (planned && !blocks.OfType<ParagraphBlock>()
                .Any(paragraph => ParagraphLines(source, paragraph)
                    .Any(static line => line.Contains("**PLANNED**", StringComparison.Ordinal))))
            {
                throw new InvalidDataException("A planned feature must explicitly declare PLANNED status.");
            }

            var requirements = 0;
            for (var index = 0; index < blocks.Length; index++)
            {
                if (blocks[index] is not HeadingBlock { Level: 3 } heading ||
                    !HeadingText(source, heading).StartsWith("FEAT-", StringComparison.Ordinal))
                {
                    continue;
                }

                var title = HeadingText(source, heading);
                var id = title.Split(' ', 2)[0];
                if (id.Length <= feature.Length + 1 ||
                    !id.StartsWith(feature + "-", StringComparison.Ordinal) ||
                    !ValidSuffix(id.AsSpan(feature.Length + 1)) ||
                    !claimed.Add(id))
                {
                    throw new InvalidDataException("Duplicate, malformed or cross-feature requirement ID.");
                }

                requirements++;
                if (planned)
                {
                    counts.PlannedRequirements++;
                }
                else
                {
                    counts.ActiveRequirements++;
                    active.Add(id);
                }

                if (planned && !strictPlanned)
                {
                    continue;
                }

                var security = false;
                var acceptance = false;
                for (var next = index + 1; next < blocks.Length; next++)
                {
                    if (blocks[next] is HeadingBlock { Level: 2 or 3 })
                    {
                        break;
                    }

                    if (blocks[next] is ParagraphBlock paragraph)
                    {
                        foreach (var line in ParagraphLines(source, paragraph))
                        {
                            security |= line.StartsWith("**Security:**", StringComparison.Ordinal);
                            acceptance |= line.StartsWith("**Acceptance:**", StringComparison.Ordinal);
                        }
                    }
                }

                if (!security || !acceptance)
                {
                    throw new InvalidDataException(planned
                        ? "Strict planned requirement needs Security and Acceptance lines."
                        : "Active requirement needs Security and Acceptance evidence.");
                }
            }

            if (structural && requirements == 0)
            {
                throw new InvalidDataException(planned
                    ? "Strict planned feature document must own a requirement."
                    : "Active feature document must own a requirement.");
            }
        }
    }

    // Instruments and Diagnostics are declared in backticks under bold labels; declarations must be
    // unique across all feature documents. Active documents must always state their instruments.
    private static void CheckDeclarations(string source, Block[] blocks, bool structural, HashSet<string> declared)
    {
        var section = string.Empty;
        var labels = new HashSet<string>(StringComparer.Ordinal);
        string? instruments = null;
        foreach (var block in blocks)
        {
            if (block is HeadingBlock { Level: 2 } heading)
            {
                section = HeadingText(source, heading);
                continue;
            }

            if (block is not ParagraphBlock paragraph)
            {
                continue;
            }

            foreach (var line in ParagraphLines(source, paragraph))
            {
                if (line.StartsWith("**Diagnostics:**", StringComparison.Ordinal))
                {
                    Declare(line["**Diagnostics:**".Length..], "diagnostic", declared);
                }

                if (section != "Metrics Impact")
                {
                    continue;
                }

                foreach (var label in MetricLabels)
                {
                    var prefix = $"**{label}:**";
                    if (!line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    labels.Add(label);
                    if (label == "Instruments")
                    {
                        instruments = line[prefix.Length..];
                        Declare(instruments, "instrument", declared);
                    }
                }
            }
        }

        if (!structural)
        {
            return;
        }

        if (instruments is null)
        {
            throw new InvalidDataException("Metrics Impact needs an **Instruments:** line (use 'none' with a reason).");
        }

        if (IsNone(instruments))
        {
            if (instruments.Trim()[4..].Trim(' ', '-', '\u2013', '\u2014', ':', '.').Length == 0)
            {
                throw new InvalidDataException("An **Instruments:** none declaration needs a reason.");
            }

            return;
        }

        if (MetricLabels.Any(label => !labels.Contains(label)))
        {
            throw new InvalidDataException("Metrics Impact with instruments needs Instruments, Emission points, Tags, Privacy, Dashboards and Tests labels.");
        }
    }

    private static void Declare(string value, string kind, HashSet<string> declared)
    {
        if (IsNone(value))
        {
            return;
        }

        var names = BacktickSpans(value);
        if (names.Count == 0)
        {
            throw new InvalidDataException($"Each {kind} declaration needs at least one backticked name or 'none'.");
        }

        foreach (var name in names)
        {
            if (!declared.Add(kind + ":" + name))
            {
                throw new InvalidDataException($"Duplicate {kind} declaration across feature documents.");
            }
        }
    }

    private static bool IsNone(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("none", StringComparison.OrdinalIgnoreCase) &&
            (trimmed.Length == 4 || !char.IsLetterOrDigit(trimmed[4]));
    }

    private static List<string> BacktickSpans(string value)
    {
        var spans = new List<string>();
        var start = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '`')
            {
                continue;
            }

            if (start < 0)
            {
                start = index + 1;
                continue;
            }

            var name = value[start..index].Trim();
            if (name.Length > 0)
            {
                spans.Add(name);
            }

            start = -1;
        }

        return spans;
    }

    // Design and Implementation Plan are the only extension sections: optional, once each, in that
    // order, between Requirements and Metrics Impact.
    private static bool HasValidSections(string[] sections)
    {
        var core = sections.Where(static name => !ExtensionSections.Contains(name, StringComparer.Ordinal)).ToArray();
        if (!core.SequenceEqual(ActiveSections))
        {
            return false;
        }

        var extensions = sections.Where(static name => ExtensionSections.Contains(name, StringComparer.Ordinal)).ToArray();
        if (!extensions.SequenceEqual(ExtensionSections.Where(extensions.Contains)))
        {
            return false;
        }

        var requirements = Array.IndexOf(sections, "Requirements");
        var metrics = Array.IndexOf(sections, "Metrics Impact");
        return extensions.All(name =>
        {
            var index = Array.IndexOf(sections, name);
            return index > requirements && index < metrics;
        });
    }

    private static string HeadingText(string source, HeadingBlock heading)
    {
        var start = heading.Span.Start;
        var end = source.IndexOf('\n', start);
        var text = source[start..(end < 0 ? source.Length : end)].TrimEnd('\r');
        if (text.StartsWith(new string('#', heading.Level) + " ", StringComparison.Ordinal))
        {
            return text[(heading.Level + 1)..].Trim();
        }

        return text.Trim();
    }

    private static IEnumerable<string> ParagraphLines(string source, ParagraphBlock paragraph)
    {
        var text = source.AsSpan(paragraph.Span.Start, paragraph.Span.Length);
        foreach (var line in text.ToString().Split('\n'))
        {
            yield return line.TrimEnd('\r');
        }
    }

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool ValidSuffix(ReadOnlySpan<char> suffix)
    {
        if (suffix.Length is < 1 or > 128 ||
            !Alphanumeric(suffix[0]) || !Alphanumeric(suffix[^1]))
        {
            return false;
        }

        foreach (var character in suffix)
        {
            if (!Alphanumeric(character) && character is not ('-' or '_' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Alphanumeric(char value) =>
        value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}

public sealed record RequirementSummary(
    int ActiveDocuments, int ActiveRequirements, int PlannedDocuments, int PlannedRequirements);
