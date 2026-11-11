using Markdig;
using Markdig.Extensions.Tables;
using System.Text.RegularExpressions;

namespace Mauve.Governance.Requirements;

public enum ValidationMethod
{
    Missing,
    Manual,
    PortableAutomated,
    EnvironmentGatedAutomated,
    Waived
}

public sealed record ValidationGap(string RequirementId, ValidationMethod Method,
    string EvidenceOrProcedure, string Owner, string Prerequisites, string Details);

public static class ValidationGaps
{
    private static readonly string[] Columns =
    [
        "Requirement", "Validation method", "Evidence or procedure", "Owner",
        "Environment / prerequisites", "Gap or waiver details"
    ];
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();
    private static readonly Regex Waiver = new(
        @"^approver:\s*(?<approver>[^;]+);\s*reason:\s*(?<reason>[^;]+);\s*expiry:\s*(?<expiry>\d{4}-\d{2}-\d{2})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static IReadOnlyList<ValidationGap> Validate(string root,
        IEnumerable<string>? sourceRequirementIds = null, DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var active = RequirementDocuments.GetActiveRequirementIds(root);
        var annotated = new HashSet<string>(sourceRequirementIds ?? [], StringComparer.Ordinal);
        if (active.Count > 0 && !annotated.IsSubsetOf(active))
        {
            throw new InvalidDataException("Source traceability contains an unknown or planned requirement ID.");
        }

        var docs = Path.Combine(root, "docs");
        var path = Path.Combine(docs, "validation-gaps.md");
        if (!Directory.Exists(docs))
        {
            if (!Directory.Exists(root) || IsLink(root))
            {
                throw new InvalidDataException("Repository root is missing or linked.");
            }

            return Array.Empty<ValidationGap>();
        }

        if (IsLink(root) || IsLink(docs) ||
            !File.Exists(path) || IsLink(path))
        {
            throw new InvalidDataException("Validation gaps ledger is missing or linked.");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var source = RequirementDocuments.ReadBounded(stream);
        var entries = new List<ValidationGap>();
        var tables = Markdown.Parse(source, Pipeline).OfType<Table>().ToArray();
        var ledgerTables = 0;
        foreach (var table in tables)
        {
            var lines = source.AsSpan(table.Span.Start, table.Span.Length).ToString()
                .Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
            var heading = Cells(lines[0]);
            if (heading is not null && heading.Length > 0 &&
                heading[0].Equals("Requirement", StringComparison.Ordinal) &&
                !heading.SequenceEqual(Columns))
            {
                throw new InvalidDataException("Validation gaps ledger columns are malformed.");
            }

            if (heading is null || !heading.SequenceEqual(Columns))
            {
                continue;
            }

            ledgerTables++;
            if (lines.Length < 2 || Cells(lines[1]) is not { Length: 6 } divider ||
                divider.Any(cell => !Regex.IsMatch(cell, @"^:?-{3,}:?$")))
            {
                throw new InvalidDataException("Validation gaps ledger has a malformed table separator.");
            }

            foreach (var line in lines.Skip(2).Where(line => !string.IsNullOrWhiteSpace(line)))
            {
                var cells = Cells(line);
                if (cells is not { Length: 6 } || cells.Any(cell => cell.Contains('\n')))
                {
                    throw new InvalidDataException("Validation gaps ledger has a malformed row.");
                }

                entries.Add(ParseRow(cells, root, active, annotated, today ?? DateOnly.FromDateTime(DateTime.UtcNow)));
            }
        }

        if (ledgerTables != 1)
        {
            throw new InvalidDataException("Validation gaps ledger must contain exactly one six-column table.");
        }

        foreach (var id in active)
        {
            var rows = entries.Where(entry => entry.RequirementId == id).ToArray();
            if (rows.Length == 0 || rows.All(entry => entry.Method == ValidationMethod.Missing))
            {
                throw new InvalidDataException($"Requirement {id} has no declared validation method or valid waiver.");
            }
        }

        return entries.AsReadOnly();
    }

    private static ValidationGap ParseRow(string[] cells, string root, IReadOnlySet<string> active,
        IReadOnlySet<string> annotated, DateOnly today)
    {
        var (id, methodText, evidence, owner, prerequisites, details) =
            (cells[0], cells[1], cells[2], cells[3], cells[4], cells[5]);
        if (!active.Contains(id))
        {
            throw new InvalidDataException($"Unknown or planned requirement ID in validation gaps ledger: {id}.");
        }

        var method = methodText.ToLowerInvariant() switch
        {
            "missing" => ValidationMethod.Missing,
            "manual" => ValidationMethod.Manual,
            "portable automated" => ValidationMethod.PortableAutomated,
            "environment-gated automated" => ValidationMethod.EnvironmentGatedAutomated,
            "waived" => ValidationMethod.Waived,
            _ => throw new InvalidDataException($"Unknown validation method for {id}.")
        };
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new InvalidDataException($"Validation method for {id} requires an owner.");
        }

        if (method == ValidationMethod.Waived)
        {
            var match = Waiver.Match(details);
            if (!match.Success || string.IsNullOrWhiteSpace(match.Groups["approver"].Value) ||
                string.IsNullOrWhiteSpace(match.Groups["reason"].Value) ||
                !DateOnly.TryParseExact(match.Groups["expiry"].Value, "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var expiry) || expiry < today)
            {
                throw new InvalidDataException($"Waiver for {id} requires an approver, reason and unexpired ISO expiry.");
            }
        }
        else if (method == ValidationMethod.Missing)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                throw new InvalidDataException($"Missing validation for {id} requires gap details.");
            }
        }
        else
        {
            if (method == ValidationMethod.EnvironmentGatedAutomated &&
                string.IsNullOrWhiteSpace(prerequisites))
            {
                throw new InvalidDataException($"Environment-gated validation for {id} requires prerequisites.");
            }

            // Source annotations establish coverage, not an outcome; only alternative methods need a local procedure.
            if (!annotated.Contains(id))
            {
                ValidateProcedure(root, evidence, id, method == ValidationMethod.PortableAutomated);
            }
        }

        return new ValidationGap(id, method, evidence, owner, prerequisites, details);
    }

    private static void ValidateProcedure(string root, string evidence, string id, bool requireNonCSharp)
    {
        var value = evidence.Trim();
        if (value.StartsWith('`') && value.EndsWith('`') && value.Length > 2)
        {
            value = value[1..^1];
        }
        else if (value.StartsWith('['))
        {
            var match = Regex.Match(value, @"^\[[^\]]+\]\(([^()\s]+)\)$");
            if (!match.Success)
            {
                throw new InvalidDataException($"Validation procedure for {id} must be a local file link.");
            }

            value = match.Groups[1].Value;
        }

        var segments = value.Split('/');
        if (value.Length == 0 || value.Contains('\\') || value.Contains(':') || value.Contains('%') ||
            value.Contains('#') || value.Contains('?') || Path.IsPathRooted(value) ||
            segments.Any(segment => segment is "" or "." or "..") ||
            requireNonCSharp && !new[] { ".sh", ".py", ".js", ".mjs", ".ps1", ".rb", ".go" }
                .Contains(Path.GetExtension(value), StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Validation procedure for {id} must be a safe repository-local file of the expected kind.");
        }

        var current = Path.GetFullPath(root);
        if (!Directory.Exists(current) || IsLink(current))
        {
            throw new InvalidDataException("Repository root is missing or linked.");
        }

        foreach (var segment in segments[..^1])
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) || IsLink(current))
            {
                throw new InvalidDataException($"Validation procedure for {id} has a missing or linked directory.");
            }
        }

        current = Path.Combine(current, segments[^1]);
        if (!File.Exists(current) || IsLink(current))
        {
            throw new InvalidDataException($"Validation procedure for {id} is missing or linked.");
        }
    }

    private static string[]? Cells(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
        {
            return null;
        }

        var cells = new List<string>();
        var start = 1;
        var code = false;
        for (var index = 1; index < trimmed.Length; index++)
        {
            if (trimmed[index] == '`' && (index == 0 || trimmed[index - 1] != '\\'))
            {
                code = !code;
            }
            else if (trimmed[index] == '|' && !code && trimmed[index - 1] != '\\')
            {
                cells.Add(trimmed[start..index].Trim());
                start = index + 1;
            }
        }

        return code || start != trimmed.Length ? null : cells.ToArray();
    }

    private static bool IsLink(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}
