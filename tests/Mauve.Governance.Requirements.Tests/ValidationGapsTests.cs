using Mauve.Governance.Requirements;
using Xunit;

namespace Mauve.Governance.Requirements.Tests;

public sealed class ValidationGapsTests
{
    private const string Feature = """
        # FEAT-1: Example
        ## Status
        **PARTIAL**
        ## Capability
        Example
        ## Context
        Example
        ## Requirements
        ### FEAT-1-001 - First
        **Security:** Safe
        **Acceptance:** Checked
        ### FEAT-1-002 - Second
        **Security:** Safe
        **Acceptance:** Checked
        ## Metrics Impact
        None
        ## Verification
        Test
        ## Diagrams
        None
        """;
    private const string Header = """
        | Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
        | --- | --- | --- | --- | --- | --- |
        """;
    private const string Manual = "| FEAT-1-001 | manual | `procedures/check.md` | Team |  | Pending run |";
    private const string Gated = "| FEAT-1-002 | environment-gated automated | [runner](procedures/check.sh) | Team | Hosted runners | Pending run |";

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Inventory_excludes_planned_and_respects_structural_headings()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature.Replace("## Metrics Impact", "```\n### FEAT-1-999 - Not active\n```\n## Metrics Impact"));
        File.WriteAllText(Path.Combine(fixture.Root, "docs/Requirements/Planned/FEAT-1-Later.md"),
            "# FEAT-1: Later\n## Status\n**PLANNED**\n### FEAT-1-003 - Future\n");
        Assert.Equal(["FEAT-1-001", "FEAT-1-002"], RequirementDocuments.GetActiveRequirementIds(fixture.Root).Order());
        fixture.WriteLedger(Manual, Gated);
        Assert.Equal(2, ValidationGaps.Validate(fixture.Root).Count);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Alternative_methods_and_multiple_rows_do_not_claim_outcomes()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger(Manual, Gated, "| FEAT-1-001 | missing |  | Team |  | Additional test unavailable |");
        var rows = ValidationGaps.Validate(fixture.Root);
        Assert.Equal([ValidationMethod.Manual, ValidationMethod.EnvironmentGatedAutomated, ValidationMethod.Missing],
            rows.Select(row => row.Method));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Source_annotation_set_can_back_a_symbolic_test_class_without_a_procedure()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger("| FEAT-1-001 | portable automated | `Mauve.Governance.TestResults.Tests` | Team | .NET test runner | Not run |", Gated);
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
        Assert.Equal(2, ValidationGaps.Validate(fixture.Root, ["FEAT-1-001"]).Count);
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root, ["FEAT-1-003"]));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Portable_script_is_an_alternative_when_source_annotations_are_absent()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger(Manual,
            "| FEAT-1-002 | portable automated | `procedures/check.sh` | Team | .NET test runner | Not run |");
        Assert.Equal(2, ValidationGaps.Validate(fixture.Root).Count);
        File.Delete(Path.Combine(fixture.Root, "procedures/check.sh"));
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
    }

    [Theory]
    [InlineData("| FEAT-1-003 | manual | `procedures/check.md` | Team |  | Pending |", "unknown")]
    [InlineData("| FEAT-1-001 | missing |  | Team |  | Pending |", "missing")]
    [InlineData("| FEAT-1-001 | manual | `../secret.md` | Team |  | Pending |", "traversal")]
    [InlineData("| FEAT-1-001 | manual | `procedures/not-here.md` | Team |  | Pending |", "nonexistent")]
    [InlineData("| FEAT-1-001 | portable automated | `procedures/check.cs` | Team |  | Pending |", "C# without source annotations")]
    [InlineData("| FEAT-1-001 | manual | `procedures/check.md` |  |  | Pending |", "no owner")]
    [InlineData("| FEAT-1-001 | manual | `procedures/check.md` | Team | Pending |", "malformed")]
    [InlineData("| FEAT-1-001 | environment-gated automated | `procedures/check.sh` | Team |  | Pending |", "no prerequisites")]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Invalid_rows_fail_closed(string row, string _)
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger(row, Gated);
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
    }

    [Theory]
    [InlineData("approver: Lead; reason: Reviewed; expiry: 2030-01-01", true)]
    [InlineData("approver: Lead; reason: Reviewed; expiry: 2029-12-31", false)]
    [InlineData("approver: ; reason: Reviewed; expiry: 2030-01-01", false)]
    [InlineData("approver: Lead; reason: Reviewed; expiry: 2030-02-31", false)]
    [InlineData("reason: Reviewed; expiry: 2030-01-01", false)]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Waivers_require_complete_unexpired_approval(string details, bool valid)
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger($"| FEAT-1-001 | waived |  | Team |  | {details} |", Gated);
        if (valid)
        {
            Assert.Equal(ValidationMethod.Waived,
                ValidationGaps.Validate(fixture.Root, today: new DateOnly(2030, 1, 1))[0].Method);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() =>
                ValidationGaps.Validate(fixture.Root, today: new DateOnly(2030, 1, 1)));
        }
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Linked_procedures_and_ledgers_and_fenced_tables_are_rejected()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        File.CreateSymbolicLink(Path.Combine(fixture.Root, "procedures/linked.md"),
            Path.Combine(fixture.Root, "procedures/check.md"));
        fixture.WriteLedger(Manual.Replace("check.md", "linked.md"), Gated);
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
        fixture.WriteLedger(Manual, Gated);
        var ledger = Path.Combine(fixture.Root, "docs/validation-gaps.md");
        File.Delete(ledger);
        File.CreateSymbolicLink(ledger, Path.Combine(fixture.Root, "procedures/check.md"));
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
        File.Delete(ledger);
        File.WriteAllText(ledger, "```\n" + Header + "\n" + Manual + "\n" + Gated + "\n```\n");
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Malformed_extra_row_cannot_hide_behind_complete_inventory()
    {
        using var fixture = new Fixture();
        fixture.WriteFeature(Feature);
        fixture.WriteLedger(Manual, Gated, "| FEAT-1-001 | manual | `procedures/check.md` | Team |");
        Assert.Throws<InvalidDataException>(() => ValidationGaps.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Core_without_docs_has_empty_inventory_and_ledger()
    {
        using var fixture = new Fixture();
        Directory.Delete(Path.Combine(fixture.Root, "docs"), recursive: true);
        Assert.Empty(RequirementDocuments.GetActiveRequirementIds(fixture.Root));
        Assert.Empty(ValidationGaps.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Core_with_docs_but_no_requirements_accepts_an_empty_ledger()
    {
        using var fixture = new Fixture();
        Directory.Delete(Path.Combine(fixture.Root, "docs/Requirements"), recursive: true);
        fixture.WriteLedger();
        Assert.Empty(RequirementDocuments.GetActiveRequirementIds(fixture.Root));
        Assert.Empty(ValidationGaps.Validate(fixture.Root, ["FEAT-Example-001"]));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetFullPath("."), "gaps-test-" + Guid.NewGuid().ToString("N"));

        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "docs/Requirements/Planned"));
            Directory.CreateDirectory(Path.Combine(Root, "procedures"));
            File.WriteAllText(Path.Combine(Root, "docs/Requirements/README.md"), "Active");
            File.WriteAllText(Path.Combine(Root, "docs/Requirements/Planned/README.md"), "Planned");
            File.WriteAllText(Path.Combine(Root, "procedures/check.md"), "# Manual procedure");
            File.WriteAllText(Path.Combine(Root, "procedures/check.sh"), "#!/bin/sh\n");
            File.WriteAllText(Path.Combine(Root, "procedures/check.cs"), "class Check {}");
        }

        public void WriteFeature(string content) =>
            File.WriteAllText(Path.Combine(Root, "docs/Requirements/FEAT-1-Example.md"), content);

        public void WriteLedger(params string[] rows) =>
            File.WriteAllText(Path.Combine(Root, "docs/validation-gaps.md"), Header + "\n" + string.Join('\n', rows) + "\n");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
