using System.Security.Cryptography;
using System.Text.Json;
using Mauve.Governance.Tool;

namespace Mauve.Governance.TestResults.Tests;

public sealed class RepositorySetupTests
{
    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Bootstrap_updates_only_owned_files_and_preserves_unmanaged_files()
    {
        using var directory = new RepositoryDirectory();
        Assert.Equal(0, Init(directory));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: true));
        var manifest = File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json"));
        Assert.StartsWith("{\n  \"SchemaVersion\": 2,\n  \"BundleVersion\": \"0.12.0\",", manifest);
        Assert.Contains("\n      \"Path\":", manifest);
        Assert.Contains("\"Profile\": \"core\"", manifest);
        Assert.EndsWith("}\n", manifest);
        Assert.Contains("\"version\": \"0.12.0\"", File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".config/dotnet-tools.json")));
        var workflow = File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".github/workflows/governance-policy.yml"));
        Assert.Contains("contents: read", workflow);
        Assert.Contains("governance -- validate --root .", workflow);
        Assert.Contains("governance -- generate --root . --output artifacts/traceability.json", workflow);
        Assert.Contains("traceability-pr-delta.patch", workflow);
        Assert.Contains("actions/upload-artifact@", workflow);
        var updaterWorkflow = File.ReadAllText(System.IO.Path.Combine(
            directory.Path, ".github/workflows/governance-update.yml"));
        Assert.Contains("governance-update", updaterWorkflow);
        Assert.Contains("Previous bundle:", updaterWorkflow);
        Assert.Contains("Selected profile:", updaterWorkflow);
        Assert.Contains("Managed-file diff", updaterWorkflow);
        Assert.Contains("Manual obligations:", updaterWorkflow);
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/update.sh")));
        foreach (var document in new[]
        {
            "governance.md",
            "docs/governance.md",
            "style-guide.md",
            ".github/agents/governance-evidence.agent.md",
            ".github/agents/governance-maintainer.agent.md",
            ".github/skills/governance-bootstrap/SKILL.md",
            ".github/skills/governance-analyzer-remediation/SKILL.md",
            ".github/skills/governance-test-evidence/SKILL.md"
        })
        {
            var text = File.ReadAllText(System.IO.Path.Combine(directory.Path, document));
            Assert.Contains("do not edit directly", text);
            Assert.Contains("https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md", text);
        }
        var policy = System.IO.Path.Combine(directory.Path, "governance.md");
        Assert.Contains("tools: [\"read\", \"search\"]", File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".github/agents/governance-maintainer.agent.md")));

        var managed = System.IO.Path.Combine(directory.Path, ".github/agents/governance-evidence.agent.md");
        File.AppendAllText(managed, "\nconsumer edit\n");
        File.AppendAllText(policy, "\nconsumer policy edit\n");
        var canonical = System.IO.Path.Combine(directory.Path, "docs/governance.md");
        File.AppendAllText(canonical, "\nconsumer canonical edit\n");
        Assert.Equal(1, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));

        var unmanaged = System.IO.Path.Combine(directory.Path, ".github/agents/consumer.agent.md");
        File.WriteAllText(unmanaged, "consumer-owned");
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.DoesNotContain("consumer edit", File.ReadAllText(managed));
        Assert.DoesNotContain("consumer policy edit", File.ReadAllText(policy));
        Assert.DoesNotContain("consumer canonical edit", File.ReadAllText(canonical));
        Assert.Equal("consumer-owned", File.ReadAllText(unmanaged));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Installed_consumer_policy_explains_lifecycle_gaps_and_current_enforcement()
    {
        using var directory = new RepositoryDirectory();
        Init(directory);
        var policy = File.ReadAllText(System.IO.Path.Combine(
            directory.Path, "governance.md"));

        Assert.Contains("Managed file: do not edit directly", policy);
        Assert.Contains("https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md", policy);
        Assert.DoesNotContain("consumer-owned", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repository-owned", policy, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[]
        {
            "## MGOV-POL-001 - Enrollment and policy ownership",
            "## MGOV-POL-002 - Features, planned work and architecture decisions",
            "## MGOV-POL-003 - Validation gaps and test evidence",
            "## MGOV-POL-004 - Repository-specific policies and annotations",
            "## MGOV-POL-005 - Fail-closed behavior and truthful status",
            "## MGOV-POL-006 - Security, performance and design quality",
            "## MGOV-POL-007 - Supply-chain integrity",
            "## MGOV-POL-008 - Structured logging and troubleshooting",
            "## MGOV-POL-009 - Managed validation and update workflows"
        }, policy.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal)));
        var sections = policy.Split("\n## MGOV-POL-", StringSplitOptions.None).Skip(1);
        foreach (var section in sections)
        {
            Assert.Equal(1, section.Split("### Testability and agent instruction",
                StringSplitOptions.None).Length - 1);
            Assert.Contains("**Automatically testable:**", section);
            Assert.Contains("**Agent instruction:**", section);
        }
        Assert.Contains("docs/Requirements/Planned/FEAT-YYYY-", policy);
        Assert.Contains("docs/Architecture/ADR-ZZZ-", policy);
        Assert.Contains("(docs/validation-gaps.md)", policy);
        Assert.Contains("(docs/Requirements/README.md)", policy);
        Assert.Contains("(docs/Policies/README.md)", policy);
        Assert.Contains("docs/Policies/POL-XXXX-Name-Of-Policy.md", policy);
        Assert.Contains("manual-run reports", policy);
        Assert.Contains("report-only", policy);
        Assert.Contains("not implemented", policy);
        Assert.Contains("merged release-gap report are not implemented.", policy);
        Assert.Contains("The Tool does not parse or enforce", policy);
        Assert.Contains("does not run that build or validate the diagnostic catalog", policy);
        Assert.Contains("Reject the affected operation on invalid or untrusted input", policy);
        Assert.Contains("failed authorization", policy);
        Assert.Contains("Preserve valid state and report", policy);
        Assert.Contains("repository-wide obligations", policy);
        Assert.Contains("security and performance outrank readability", policy);
        Assert.Contains("SBOM", policy);
        Assert.Contains("nonzero, stable numeric event ID", policy);
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Existing_unmanaged_file_blocks_enrollment()
    {
        using var directory = new RepositoryDirectory();
        var collision = System.IO.Path.Combine(directory.Path, "governance.md");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(collision)!);
        File.WriteAllText(collision, "consumer owns this");
        Assert.Throws<InvalidDataException>(() =>
            Init(directory));
        Assert.Equal("consumer owns this", File.ReadAllText(collision));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Installed_style_and_agent_assets_match_embedded_templates_and_owned_hashes()
    {
        using var directory = new RepositoryDirectory();
        Assert.Equal(0, Init(directory));
        using var manifest = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(
            directory.Path, ".mauve-governance/manifest.json")));
        var owned = manifest.RootElement.GetProperty("Files").EnumerateArray().ToDictionary(
            file => file.GetProperty("Path").GetString()!,
            file => file.GetProperty("Sha256").GetString()!,
            StringComparer.Ordinal);
        var templateRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            AppContext.BaseDirectory, "../../../../../src/templates"));

        foreach (var path in new[] { ".editorconfig", "AGENTS.md", "style-guide.md",
                     "docs/governance.md", "docs/Policies/README.md" })
        {
            var source = File.ReadAllBytes(System.IO.Path.Combine(templateRoot, path));
            var installed = File.ReadAllBytes(System.IO.Path.Combine(directory.Path, path));
            Assert.Equal(source, installed);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(installed)), owned[path]);
        }

        var editorconfig = File.ReadAllText(System.IO.Path.Combine(directory.Path, ".editorconfig"));
        foreach (var diagnostic in new[] { "MGTEST001", "MGTEST002", "MGTEST003", "MGTEST004",
                     "MGLOG001", "MGLOG002" })
        {
            Assert.Contains($"dotnet_diagnostic.{diagnostic}.severity = error", editorconfig);
        }

        var agents = File.ReadAllText(System.IO.Path.Combine(directory.Path, "AGENTS.md"));
        Assert.Contains("do not edit directly", agents);
        Assert.Contains("https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md", agents);
        Assert.Contains("(governance.md)", agents);
        Assert.Contains("(style-guide.md)", agents);
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "docs/Requirements/README.md")));
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "docs/Requirements/Planned/README.md")));
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "docs/validation-gaps.md")));
        Assert.DoesNotContain("docs/validation-gaps.md", owned.Keys);

        var guide = File.ReadAllText(System.IO.Path.Combine(
            directory.Path, "style-guide.md"));
        Assert.Contains("do not edit directly", guide);
        Assert.Contains("https://github.com/mauve/Mauve.Governance/blob/main/src/templates/docs/governance.md", guide);
        Assert.Contains("(governance.md)", guide);
        var canonical = File.ReadAllText(System.IO.Path.Combine(directory.Path, "docs/governance.md"));
        Assert.Contains("## Framework requirements", canonical);
        Assert.Contains("MG-POL-001 - Ownership and profiles", canonical);
        Assert.Contains("do not edit directly", canonical);
        Assert.Equal(new[]
        {
            "## MSTY-POL-001 - General style",
            "## MSTY-POL-002 - Formatting and naming",
            "## MSTY-POL-003 - C# source",
            "## MSTY-POL-004 - Exceptions and error context",
            "## MSTY-POL-005 - Resource ownership",
            "## MSTY-POL-006 - Comments and documentation",
            "## MSTY-POL-007 - Tests"
        }, guide.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal)));
        Assert.DoesNotContain("Mauve.Fleet", guide);
        Assert.DoesNotContain("FEAT-11", guide);
        Assert.DoesNotContain("BAM", guide);
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Theory]
    [InlineData(".editorconfig")]
    [InlineData("AGENTS.md")]
    [InlineData("style-guide.md")]
    [InlineData("docs/governance.md")]
    [InlineData("docs/Policies/README.md")]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Unmanaged_style_and_agent_assets_block_enrollment(string path)
    {
        using var directory = new RepositoryDirectory();
        var collision = System.IO.Path.Combine(directory.Path, path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(collision)!);
        File.WriteAllText(collision, "consumer owns this");
        Assert.Throws<InvalidDataException>(() => Init(directory));
        Assert.Equal("consumer owns this", File.ReadAllText(collision));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Editable_scaffolds_preserve_existing_guidance_and_evidence()
    {
        using var directory = new RepositoryDirectory();
        var requirements = System.IO.Path.Combine(directory.Path, "docs/Requirements");
        Directory.CreateDirectory(requirements);
        File.WriteAllText(System.IO.Path.Combine(requirements, "README.md"), "Local lifecycle");
        var ledger = System.IO.Path.Combine(directory.Path, "docs/validation-gaps.md");
        File.WriteAllText(ledger, "Existing requirement evidence");

        Assert.Equal(0, Init(directory));
        Assert.Equal("Local lifecycle", File.ReadAllText(
            System.IO.Path.Combine(requirements, "README.md")));
        Assert.Equal("Existing requirement evidence", File.ReadAllText(ledger));
        Assert.True(File.Exists(System.IO.Path.Combine(requirements, "Planned/README.md")));
        Assert.DoesNotContain("docs/Requirements/README.md",
            File.ReadAllText(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
        Assert.DoesNotContain("docs/validation-gaps.md",
            File.ReadAllText(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));
        File.WriteAllText(ledger, """
            | Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
            | --- | --- | --- | --- | --- | --- |
            """);
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Tampered_ownership_hash_blocks_replacement()
    {
        using var directory = new RepositoryDirectory();
        Init(directory);
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        var contents = File.ReadAllText(manifest);
        var hashStart = contents.IndexOf("\"Sha256\": \"", StringComparison.Ordinal);
        Assert.True(hashStart > 0);
        var index = hashStart + "\"Sha256\": \"".Length;
        File.WriteAllText(manifest, contents[..index] + (contents[index] == 'A' ? "B" : "A") + contents[(index + 1)..]);
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("update", directory.Path, check: false));
    }

    [Theory]
    [InlineData(",\"Rules\":[\"unknown\"]")]
    [InlineData(",\"Profile\":\"core\"")]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Unknown_or_duplicate_manifest_fields_fail_closed(string inserted)
    {
        using var directory = new RepositoryDirectory();
        Init(directory);
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        var text = File.ReadAllText(manifest);
        var body = text.TrimEnd('\r', '\n');
        File.WriteAllText(manifest, body[..^1] + inserted + "}\n");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Feature_lifecycle_rejects_duplicate_promotions_and_missing_security()
    {
        using var directory = new RepositoryDirectory();
        Init(directory);
        var root = System.IO.Path.Combine(directory.Path, "docs/Requirements");
        var planned = System.IO.Path.Combine(root, "Planned");
        Directory.CreateDirectory(planned);
        File.WriteAllText(System.IO.Path.Combine(root, "README.md"), "Active features");
        File.WriteAllText(System.IO.Path.Combine(planned, "README.md"), "Proposals");
        var active = System.IO.Path.Combine(root, "FEAT-1-Capability.md");
        File.WriteAllText(active, """
            # FEAT-1: Capability
            ## Status
            **PARTIAL**
            ## Capability
            Description
            ## Context
            Description
            ## Requirements
            ### FEAT-1-001 - Condition
            **Acceptance:** Test
            **Security:** Safe
            ## Metrics Impact
            None
            ## Verification
            Tests
            ## Diagrams
            Simple feature
            """);
        var future = System.IO.Path.Combine(planned, "FEAT-1-Future.md");
        File.WriteAllText(future, "# FEAT-1 planned: Capability\n## Status\n**PLANNED**\n### FEAT-1-002 - Future");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "procedure.md"), "Manual verification steps");
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "docs/validation-gaps.md"), """
            | Requirement | Validation method | Evidence or procedure | Owner | Environment / prerequisites | Gap or waiver details |
            | --- | --- | --- | --- | --- | --- |
            | FEAT-1-001 | manual | `procedure.md` | Maintainer | Local | No run observed. |
            """);
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));

        File.WriteAllText(future, "# FEAT-1 planned: Capability\n## Status\n**PLANNED**\n### FEAT-1-001 - Duplicate");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));

        File.WriteAllText(future, "# FEAT-1 planned: Capability\n## Status\n**PLANNED**\n### FEAT-1-002 - Future");
        File.WriteAllText(active, File.ReadAllText(active).Replace("**Security:**", "Security:", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));

        File.WriteAllText(active, File.ReadAllText(active).Replace("Security:", "**Security:**", StringComparison.Ordinal));
        File.WriteAllText(future, "# FEAT-1 planned: Capability\n## Status\nFuture\n### FEAT-1-002 - Future");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Validate_requires_valid_source_metadata_without_a_committed_index()
    {
        using var directory = new RepositoryDirectory();
        Init(directory);
        var source = System.IO.Path.Combine(directory.Path, "Fixture.cs");
        File.WriteAllText(source, """
            using Xunit;
            class Fixture { [Fact] void Unannotated() {} }
            """);
        Assert.Contains("TRACE002", Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false)).Message);

        File.WriteAllText(source, """
            using Xunit;
            class Fixture {
                [Fact] [Trait("Feature", "FEAT-1")]
                void Annotated() {}
            }
            """);
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "docs/governance/traceability.json")));
    }

    [Theory]
    [InlineData(".config/dotnet-tools.json")]
    [InlineData(".editorconfig")]
    [InlineData("AGENTS.md")]
    [InlineData("style-guide.md")]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Known_legacy_bundle_migrates_without_taking_ownership_of_unknown_paths(string unmanagedPath)
    {
        using var directory = new RepositoryDirectory();
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, """
            {"SchemaVersion":1,"BundleVersion":"0.2.0","Files":[{"Path":".github/agents/governance-evidence.agent.md","Sha256":"4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F"},{"Path":".github/skills/governance-test-evidence/SKILL.md","Sha256":"273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885"},{"Path":"docs/governance/README.md","Sha256":"59097F2FDFF71F8F4F07A98F4F71D9DF06F14E8DB1D62F3479E711AB9D063E62"}]}
            """);
        var collision = System.IO.Path.Combine(directory.Path, unmanagedPath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(collision)!);
        File.WriteAllText(collision, "consumer-owned file");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Equal("consumer-owned file", File.ReadAllText(collision));
        Assert.Contains("\"BundleVersion\":\"0.2.0\"", File.ReadAllText(manifest));
        File.Delete(collision);

        Assert.Equal(1, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Contains("\"SchemaVersion\": 2", File.ReadAllText(manifest));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Known_0_3_bundle_migrates_without_implicit_new_ownership()
    {
        using var directory = new RepositoryDirectory();
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, """
            {"SchemaVersion":2,"BundleVersion":"0.3.0","Files":[{"Path":".config/dotnet-tools.json","Sha256":"C850B8B088385410200621ADA1F8E183404075F656B7365FABD361A14939CE74"},{"Path":".github/agents/governance-evidence.agent.md","Sha256":"4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F"},{"Path":".github/skills/governance-test-evidence/SKILL.md","Sha256":"273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885"},{"Path":".github/workflows/governance-policy.yml","Sha256":"AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A"},{"Path":"docs/governance/README.md","Sha256":"38F61F8CF2BA1C8CAD2BB0D0A5C1D7164F66A97D990722D1350A51BD74ED372D"}],"Profile":"core"}
            """);
        var unmanaged = System.IO.Path.Combine(directory.Path, ".github/workflows/governance-update.yml");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(unmanaged)!);
        File.WriteAllText(unmanaged, "consumer-owned");
        Assert.Throws<InvalidDataException>(() => RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Equal("consumer-owned", File.ReadAllText(unmanaged));
        File.Delete(unmanaged);
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Contains("\"BundleVersion\": \"0.12.0\"", File.ReadAllText(manifest));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Enrollment_requires_supported_explicit_profile_and_version()
    {
        using var directory = new RepositoryDirectory();
        Assert.Throws<InvalidDataException>(() => RepositorySetup.Run("init", directory.Path, check: false));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("init", directory.Path, check: false, "fleet", "0.12.0"));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("init", directory.Path, check: false, "core", "0.4.0"));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Previous_update_bundle_adds_linked_policy_without_owning_a_preexisting_file()
    {
        using var directory = new RepositoryDirectory();
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, """
            {"SchemaVersion":2,"BundleVersion":"0.4.0","Files":[{"Path":".config/dotnet-tools.json","Sha256":"387CCB1D7C330DD364C91644C1C2C703067A4B3591B7702C58BBC168826FA779"},{"Path":".github/agents/governance-evidence.agent.md","Sha256":"4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F"},{"Path":".github/skills/governance-test-evidence/SKILL.md","Sha256":"273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885"},{"Path":".github/workflows/governance-policy.yml","Sha256":"AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A"},{"Path":".github/workflows/governance-update.yml","Sha256":"6DF53213EAA26DB02048AE57C7D3E4B9C7B8A37DCF313B9A948F240C8B80F36A"},{"Path":".mauve-governance/update.sh","Sha256":"E042D0B2D844C42148A0BCF117EE84536C184A05426AAEB02F1D44DC33684E66"},{"Path":"docs/governance/README.md","Sha256":"98EA88D2F99A3D91507EC137CC7FB758A12480DD7D83263F186F4F21C7986208"}],"Profile":"core"}
            """);
        var policy = System.IO.Path.Combine(directory.Path, "governance.md");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(policy)!);
        File.WriteAllText(policy, "consumer-owned policy");
        Assert.Throws<InvalidDataException>(() => RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Equal("consumer-owned policy", File.ReadAllText(policy));
        File.Delete(policy);
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Contains("do not edit directly", File.ReadAllText(policy));
        Assert.Contains("\"BundleVersion\": \"0.12.0\"", File.ReadAllText(manifest));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Previous_review_bundle_replaces_copied_documents_but_preserves_unmanaged_files()
    {
        using var directory = new RepositoryDirectory();
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, """
            {"SchemaVersion":2,"BundleVersion":"0.7.0","Profile":"core","Files":[
            {"Path":".config/dotnet-tools.json","Sha256":"4ACB96C5D0524A8E7BCBCBE2B110A17643C7BAF75C50D4ECF0449E940B1CB077"},
            {"Path":".github/agents/governance-evidence.agent.md","Sha256":"4FB8CBAD7A48701E02004847776F98A2F407C2911EC5147D14F05189965BBE2F"},
            {"Path":".github/agents/governance-maintainer.agent.md","Sha256":"938F2830CC7B94DA3E1A7DA17EA3F83E80CFBCF87B67AAE1D072A9947A51E92C"},
            {"Path":".github/skills/governance-analyzer-remediation/SKILL.md","Sha256":"AE036E71092B9F7CCC2604A8C97C1BC0DF753F4006FE7F934D40E2633D4A604F"},
            {"Path":".github/skills/governance-bootstrap/SKILL.md","Sha256":"C0952BBC16ADCBB3F39A744A7282519B3F7397610CF5CD6CDF9737CD1AAF4830"},
            {"Path":".github/skills/governance-test-evidence/SKILL.md","Sha256":"273C4A1C39D0BBFCDE0EB1B0A8C85C251E6446B739BE40CDDD83B5373F331885"},
            {"Path":".github/workflows/governance-policy.yml","Sha256":"AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A"},
            {"Path":".github/workflows/governance-update.yml","Sha256":"63CCD50EF99985007C2F0AE860ECFBEFE5B572038C74C1944B14DEDD7CEB8FD5"},
            {"Path":".mauve-governance/update.sh","Sha256":"DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1"},
            {"Path":"docs/governance/GOVERNANCE_POLICY.md","Sha256":"CC82623997F357CC15B725A2A9363FAFEB86DD66A8A68AF670B83B3511613B6C"},
            {"Path":"docs/governance/README.md","Sha256":"1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"}]}
            """);
        var agent = System.IO.Path.Combine(directory.Path, ".github/agents/governance-maintainer.agent.md");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(agent)!);
        File.WriteAllText(agent, "outdated owned document");
        var consumer = System.IO.Path.Combine(directory.Path, ".github/agents/consumer.agent.md");
        File.WriteAllText(consumer, "consumer-owned");

        Assert.Equal(1, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Equal("outdated owned document", File.ReadAllText(agent));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Contains("do not edit directly", File.ReadAllText(agent));
        Assert.Equal("consumer-owned", File.ReadAllText(consumer));
        Assert.Contains("\"BundleVersion\": \"0.12.0\"", File.ReadAllText(manifest));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Previous_template_bundle_drops_retired_updater_ownership()
    {
        using var directory = new RepositoryDirectory();
        var manifest = System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(manifest)!);
        File.WriteAllText(manifest, """
            {"SchemaVersion":2,"BundleVersion":"0.10.0","Profile":"core","Files":[
            {"Path":".config/dotnet-tools.json","Sha256":"45449F74B352F0B8049CCD12F26818DF84825ED18BF3C7D64ECF5792799A7805"},
            {"Path":".github/agents/governance-evidence.agent.md","Sha256":"56B9945696C5EC0C5D892C66FE8B5140D86F7C71D95589CC03C0717A99E3BC2C"},
            {"Path":".github/agents/governance-maintainer.agent.md","Sha256":"2500F9499CBCF6DEF25D31EC0C6FB0D6D2F2401CEC775CAB1C87008EB0F16BD7"},
            {"Path":".github/skills/governance-analyzer-remediation/SKILL.md","Sha256":"70FEA02AD613218B23A7ED9BB90F36A1733BE529CDD503FD5377CE20D7361A4C"},
            {"Path":".github/skills/governance-bootstrap/SKILL.md","Sha256":"8C46E8E2BE90E060A939E35241301410E6244DCBC2168EC8989E34C3BBA321BB"},
            {"Path":".github/skills/governance-test-evidence/SKILL.md","Sha256":"C4B46DEF1D12C6DC00631F870F80A9697560F58D297355BCE5C4B42EA7D55162"},
            {"Path":".github/workflows/governance-policy.yml","Sha256":"AE08A0B6BFCB7860465EEDFBD6918CA2CB25B9737AD04115ABF1236905A8FD2A"},
            {"Path":".github/workflows/governance-update.yml","Sha256":"65952EE0C3490BE1D1E892B1A9C6A54488BCE9EFB23B6BCCDC585ED098861760"},
            {"Path":".mauve-governance/update.sh","Sha256":"DEF0FF607B819E0527F2E72E69E258D38AFC3F28194E2AB456E69016234722D1"},
            {"Path":"docs/governance/GOVERNANCE_POLICY.md","Sha256":"CD5333E5317F99ADFEEAA1D5D3F35BB8154231EA8A40FEEC7A68C5BFC8C41162"},
            {"Path":"docs/governance/README.md","Sha256":"1E848B4678BFA3ED31B47E6F8CFF247FFDFC62EE634D9559A34CFA21167FAFDB"}]}
            """);
        var script = System.IO.Path.Combine(directory.Path, ".mauve-governance/update.sh");
        File.WriteAllText(script, "consumer edit to retired updater");
        Assert.Throws<InvalidDataException>(() => RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Equal("consumer edit to retired updater", File.ReadAllText(script));
        Assert.Contains("\"BundleVersion\":\"0.10.0\"", File.ReadAllText(manifest));
        File.Delete(script);
        Assert.Equal(1, RepositorySetup.Run("update", directory.Path, check: true));
        Assert.Contains("\"BundleVersion\":\"0.10.0\"", File.ReadAllText(manifest));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.DoesNotContain(".mauve-governance/update.sh", File.ReadAllText(manifest));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/update.sh")));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Unowned_updater_is_not_deleted()
    {
        using var directory = new RepositoryDirectory();
        var script = System.IO.Path.Combine(directory.Path, ".mauve-governance/update.sh");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "consumer-owned script");
        Assert.Equal(0, Init(directory));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Equal("consumer-owned script", File.ReadAllText(script));
        Assert.DoesNotContain(".mauve-governance/update.sh", File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-002")]
    public void Core_requirements_profile_requires_the_lifecycle_and_survives_updates()
    {
        using var directory = new RepositoryDirectory();
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("init", directory.Path, check: false,
                "core-requirements", "0.12.0"));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));

        var requirements = System.IO.Path.Combine(directory.Path, "docs/Requirements");
        var planned = System.IO.Path.Combine(requirements, "Planned");
        Directory.CreateDirectory(planned);
        File.WriteAllText(System.IO.Path.Combine(requirements, "README.md"), "Active features");
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("init", directory.Path, check: false,
                "core-requirements", "0.12.0"));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".config/dotnet-tools.json")));
        File.WriteAllText(System.IO.Path.Combine(planned, "README.md"), "Proposed features");
        Assert.Equal(0, RepositorySetup.Run("init", directory.Path, check: false,
            "core-requirements", "0.12.0"));
        Assert.Equal(0, RepositorySetup.Run("validate", directory.Path, check: false));
        Assert.Contains("selected profile", File.ReadAllText(
            System.IO.Path.Combine(directory.Path, "governance.md")));
        Assert.Equal(0, RepositorySetup.Run("update", directory.Path, check: false));
        Assert.Contains("\"Profile\": \"core-requirements\"", File.ReadAllText(
            System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
        File.Delete(System.IO.Path.Combine(planned, "README.md"));
        Assert.Throws<InvalidDataException>(() =>
            RepositorySetup.Run("validate", directory.Path, check: false));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Directory_at_managed_path_blocks_enrollment_before_any_file_is_written()
    {
        using var directory = new RepositoryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(directory.Path, ".github/workflows/governance-policy.yml"));
        Assert.Throws<InvalidDataException>(() => Init(directory));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".config/dotnet-tools.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Dangling_symlink_at_managed_path_blocks_enrollment()
    {
        using var directory = new RepositoryDirectory();
        var skill = System.IO.Path.Combine(directory.Path, ".github/skills/governance-test-evidence/SKILL.md");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(skill)!);
        File.CreateSymbolicLink(skill, System.IO.Path.Combine(directory.Path, "missing.md"));
        Assert.Throws<InvalidDataException>(() => Init(directory));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, ".mauve-governance/manifest.json")));
    }

    private static int Init(RepositoryDirectory directory) =>
        RepositorySetup.Run("init", directory.Path, check: false, "core", "0.12.0");

    private sealed class RepositoryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"governance-setup-{Guid.NewGuid():N}");

        public RepositoryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
