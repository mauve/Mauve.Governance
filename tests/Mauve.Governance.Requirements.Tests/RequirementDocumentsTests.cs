using Mauve.Governance.Requirements;
using System.Text;
using Xunit;

namespace Mauve.Governance.Requirements.Tests;

public sealed class RequirementDocumentsTests
{
    private const string Active = """
        # FEAT-1: Capability
        ## Status
        **PARTIAL**
        ## Capability
        Example
        ## Context
        Example
        ## Requirements
        ### FEAT-1-001 - Condition
        **Security:** Safe
        **Acceptance:** Verified
        ## Metrics Impact
        None
        ## Verification
        Tests
        ## Diagrams
        None
        """;

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Absent_lifecycle_is_optional()
    {
        using var fixture = new FeatureDirectory();
        RequirementDocuments.Validate(fixture.Root);
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Valid_active_and_planned_requirements()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active);
        fixture.WritePlanned("# FEAT-1: Future\n## Status\n**PLANNED**\n### FEAT-1-002 - Later\n");
        RequirementDocuments.Validate(fixture.Root);
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Fenced_headings_do_not_define_sections_or_requirements()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active.Replace("## Capability", """
            ~~~markdown
            ## Fake
            ### FEAT-1-999 - Fake
            ~~~
            ## Capability
            """, StringComparison.Ordinal));
        RequirementDocuments.Validate(fixture.Root);
        fixture.WriteActive(Active.Replace("## Capability", "~~~\n## Capability\n~~~", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Fenced_evidence_and_planned_markers_do_not_count()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active.Replace("**Security:** Safe", "```\n**Security:** Safe\n```", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
        fixture.WriteActive(Active);
        fixture.WritePlanned("# FEAT-1: Future\n## Status\n```\n**PLANNED**\n```\n### FEAT-1-002 - Later\n");
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Setext_section_is_structural_and_order_is_enforced()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active.Replace("## Capability\nExample", "\nCapability\n----------\nExample", StringComparison.Ordinal));
        RequirementDocuments.Validate(fixture.Root);
        fixture.WriteActive(Active.Replace("## Capability\nExample", "\nUnknown\n-------\nExample", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Duplicate_ids_across_lifecycle_fail()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active);
        fixture.WritePlanned("# FEAT-1: Future\n## Status\n**PLANNED**\n### FEAT-1-001 - Duplicate\n");
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Rejects_oversized_and_linked_documents()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(new string('x', 1024 * 1024 + 1));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
        fixture.WriteActive(Active);
        var linked = Path.Combine(fixture.Requirements, "FEAT-1-Linked.md");
        File.CreateSymbolicLink(linked, Path.Combine(fixture.Requirements, "FEAT-1-Valid.md"));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Rejects_oversized_multibyte_utf8_content()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active + new string('é', 600_000));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Read_rejects_stream_that_grows_beyond_reported_length()
    {
        using var stream = new UnderreportedLengthStream(new byte[1024 * 1024 + 1]);
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.ReadBounded(stream));
        Assert.Equal(1024 * 1024 + 1, stream.Position);
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Utf8_bom_is_accepted_and_invalid_utf8_is_rejected()
    {
        using var fixture = new FeatureDirectory();
        File.WriteAllText(Path.Combine(fixture.Requirements, "FEAT-1-Valid.md"), Active,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        RequirementDocuments.Validate(fixture.Root);
        File.WriteAllBytes(Path.Combine(fixture.Requirements, "FEAT-1-Valid.md"), [0xC3, 0x28]);
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Rejects_missing_or_linked_lifecycle_readme()
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active);
        var readme = Path.Combine(fixture.Requirements, "README.md");
        File.Delete(readme);
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
        File.CreateSymbolicLink(readme, Path.Combine(fixture.Requirements, "Planned", "README.md"));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    [Theory]
    [InlineData("### FEAT-2-001 - Wrong feature")]
    [InlineData("### FEAT-1-! - Invalid suffix")]
    [InlineData("### FEAT-1-001 - Duplicate\n### FEAT-1-001 - Duplicate")]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Rejects_cross_feature_malformed_and_duplicate_ids(string replacement)
    {
        using var fixture = new FeatureDirectory();
        fixture.WriteActive(Active.Replace("### FEAT-1-001 - Condition", replacement, StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => RequirementDocuments.Validate(fixture.Root));
    }

    private sealed class FeatureDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetFullPath("."), "requirements-test-" + Guid.NewGuid().ToString("N"));
        public string Requirements => Path.Combine(Root, "docs", "Requirements");

        public FeatureDirectory()
        {
            Directory.CreateDirectory(Path.Combine(Requirements, "Planned"));
            File.WriteAllText(Path.Combine(Requirements, "README.md"), "Active");
            File.WriteAllText(Path.Combine(Requirements, "Planned", "README.md"), "Planned");
        }

        public void WriteActive(string text) =>
            File.WriteAllText(Path.Combine(Requirements, "FEAT-1-Valid.md"), text);

        public void WritePlanned(string text) =>
            File.WriteAllText(Path.Combine(Requirements, "Planned", "FEAT-1-Future.md"), text);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class UnderreportedLengthStream(byte[] content) : MemoryStream(content)
    {
        public override long Length => 0;
    }
}
