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

    public static void Validate(string root)
        => ValidateCore(root);

    public static IReadOnlySet<string> GetActiveRequirementIds(string root)
        => ValidateCore(root);

    private static IReadOnlySet<string> ValidateCore(string root)
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
        CheckFiles(requirementsRoot, planned: false, claimed, active);
        CheckFiles(plannedRoot, planned: true, claimed, active);
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

    private static void CheckFiles(string directory, bool planned, HashSet<string> claimed, HashSet<string> active)
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
            if (!planned && !sections.SequenceEqual(ActiveSections))
            {
                throw new InvalidDataException("Active feature sections are missing or out of order.");
            }

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
                    continue;
                }

                active.Add(id);
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
                    throw new InvalidDataException("Active requirement needs Security and Acceptance evidence.");
                }
            }

            if (!planned && requirements == 0)
            {
                throw new InvalidDataException("Active feature document must own a requirement.");
            }
        }
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
