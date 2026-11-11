using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mauve.Governance.Tool;

namespace Mauve.Governance.TestResults.Tests;

public sealed class ReleaseUpdaterTests
{
    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Same_version_is_no_op_without_downloading_or_staging()
    {
        using var fixture = new ReleaseFixture(RepositorySetup.CurrentBundleVersion);
        var result = await fixture.Updater.ApplyAsync(fixture.Root, fixture.Output);
        Assert.False(result);
        Assert.Equal("changed=false\n", File.ReadAllText(fixture.Output));
        Assert.Single(fixture.Calls);
        Assert.StartsWith("gh release view --repo mauve/Mauve.Governance ", fixture.Calls[0]);
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Downgrade_fails_without_writing_outputs()
    {
        using var fixture = new ReleaseFixture(RepositorySetup.CurrentBundleVersion) { ReleaseVersion = "0.10.0" };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.False(File.Exists(fixture.Output));
        Assert.Single(fixture.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Missing_or_modified_digest_refuses_to_install(bool tampered)
    {
        using var fixture = new ReleaseFixture("0.9.0") { Tampered = tampered, MissingDigest = !tampered };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.DoesNotContain(fixture.Calls, call => call.StartsWith("dotnet ", StringComparison.Ordinal));
        Assert.False(File.Exists(fixture.Output));
        Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Root, ".mauve-governance")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Verified_release_installs_exact_version_and_stages_only_owned_and_retired_paths()
    {
        using var fixture = new ReleaseFixture("0.9.0") { ApplyUpdate = true };
        Assert.True(await fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.Equal(4, fixture.DownloadedAssets.Count);
        Assert.Contains(fixture.DownloadedAssets, uri => uri.EndsWith("/4", StringComparison.Ordinal));
        Assert.Equal($"version={RepositorySetup.CurrentBundleVersion}\nchanged=true\n", File.ReadAllText(fixture.Output));
        Assert.Contains(fixture.Calls, call => call.StartsWith($"dotnet tool install Mauve.Governance.Tool --version {RepositorySetup.CurrentBundleVersion} ", StringComparison.Ordinal));
        Assert.Contains(fixture.Calls, call => call.EndsWith(" update --root " + fixture.Root + " --apply", StringComparison.Ordinal));
        Assert.Contains(fixture.Calls, call => call.EndsWith(" validate --root " + fixture.Root, StringComparison.Ordinal));
        Assert.Contains(fixture.Calls, call => call.EndsWith(" add -A -- .mauve-governance/update.sh", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".mauve-governance/update.sh")));
        Assert.DoesNotContain(fixture.Calls, call => call.EndsWith(" add -A -- consumer.txt", StringComparison.Ordinal));
        Assert.Equal("consumer-owned", File.ReadAllText(Path.Combine(fixture.Root, "consumer.txt")));
        Assert.Empty(Directory.GetDirectories(Path.Combine(fixture.Root, ".mauve-governance")));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Unmanaged_staged_change_blocks_output()
    {
        using var fixture = new ReleaseFixture("0.9.0") { ApplyUpdate = true, UnmanagedStaged = true };
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.False(File.Exists(fixture.Output));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Mismatched_tag_or_oversized_asset_is_rejected_before_install()
    {
        using var wrongTag = new ReleaseFixture("0.9.0") { WrongTag = true };
        await Assert.ThrowsAsync<InvalidDataException>(() => wrongTag.Updater.ApplyAsync(wrongTag.Root, wrongTag.Output));
        Assert.DoesNotContain(wrongTag.Calls, call => call.StartsWith("dotnet ", StringComparison.Ordinal));

        using var oversized = new ReleaseFixture("0.9.0") { OversizedAsset = true };
        await Assert.ThrowsAsync<InvalidDataException>(() => oversized.Updater.ApplyAsync(oversized.Root, oversized.Output));
        Assert.DoesNotContain(oversized.Calls, call => call.StartsWith("dotnet ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Manifest_version_must_match_pinned_tool_before_release_discovery()
    {
        using var fixture = new ReleaseFixture("0.9.0");
        fixture.SetInstalledVersion("0.8.0");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.Empty(fixture.Calls);
        Assert.False(File.Exists(fixture.Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Untrusted_manifest_hash_or_added_path_cannot_be_staged(bool extraPath)
    {
        using var fixture = new ReleaseFixture("0.9.0");
        var manifest = Path.Combine(fixture.Root, ".mauve-governance/manifest.json");
        var document = JsonNode.Parse(File.ReadAllText(manifest))!;
        var files = document["Files"]!.AsArray();
        if (extraPath)
        {
            files.Add(new JsonObject { ["Path"] = "consumer.txt", ["Sha256"] = new string('A', 64) });
        }
        else
        {
            files[0]!["Sha256"] = new string('A', 64);
        }
        File.WriteAllText(manifest, document.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.DoesNotContain(fixture.Calls, call => call.Contains(" add ", StringComparison.Ordinal) ||
            call.StartsWith("dotnet ", StringComparison.Ordinal));
        Assert.Equal("consumer-owned", File.ReadAllText(Path.Combine(fixture.Root, "consumer.txt")));
        Assert.False(File.Exists(fixture.Output));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Edited_owned_file_is_allowed_by_preflight_and_replaced_by_update()
    {
        using var fixture = new ReleaseFixture("0.9.0") { ApplyUpdate = true };
        var edited = Path.Combine(fixture.Root, ".github/agents/governance-evidence.agent.md");
        File.WriteAllText(edited, "consumer edit");
        Assert.True(await fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.Contains(fixture.Calls, call => call.EndsWith(" add -A -- .github/agents/governance-evidence.agent.md", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public async Task Release_stages_new_editable_scaffolds_but_not_existing_evidence()
    {
        using var fixture = new ReleaseFixture("0.9.0") { ApplyUpdate = true };
        File.Delete(Path.Combine(fixture.Root, "docs/validation-gaps.md"));
        Assert.True(await fixture.Updater.ApplyAsync(fixture.Root, fixture.Output));
        Assert.Contains(fixture.Calls, call => call.EndsWith(
            " add -A -- docs/validation-gaps.md", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Calls, call => call.EndsWith(
            " add -A -- docs/Requirements/README.md", StringComparison.Ordinal));
    }

    private sealed class ReleaseFixture : IDisposable
    {
        private static readonly string[] Packages = ["Tool", "TestResults", "Traceability", "Requirements"];
        private readonly HttpClient client;
        private readonly byte[] package = Encoding.UTF8.GetBytes("package fixture");
        public readonly List<string> Calls = [];
        public readonly List<string> DownloadedAssets = [];
        public readonly string Root = Path.Combine(Directory.GetCurrentDirectory(), "artifacts",
            "release-updater-tests", Guid.NewGuid().ToString("N"));
        public readonly string Output;
        public bool Tampered { get; set; }
        public bool MissingDigest { get; set; }
        public bool ApplyUpdate { get; set; }
        public bool UnmanagedStaged { get; set; }
        public bool WrongTag { get; set; }
        public bool OversizedAsset { get; set; }
        public string ReleaseVersion { get; set; } = RepositorySetup.CurrentBundleVersion;
        public ReleaseUpdater Updater { get; }

        public ReleaseFixture(string installedVersion)
        {
            Output = Path.Combine(Root, "output.txt");
            Directory.CreateDirectory(Root);
            RepositorySetup.Run("init", Root, check: false, profile: "core",
                bundleVersion: RepositorySetup.CurrentBundleVersion);
            if (installedVersion == "0.9.0")
            {
                var fixtures = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/fixtures"));
                File.Copy(Path.Combine(fixtures, "manifest-0.9.0.json"),
                    Path.Combine(Root, ".mauve-governance/manifest.json"), overwrite: true);
                File.Delete(Path.Combine(Root, ".editorconfig"));
                File.Delete(Path.Combine(Root, "AGENTS.md"));
                File.Delete(Path.Combine(Root, "style-guide.md"));
                File.Delete(Path.Combine(Root, "governance.md"));
                File.Delete(Path.Combine(Root, "docs/governance.md"));
                File.Delete(Path.Combine(Root, "docs/Policies/README.md"));
                File.Copy(Path.Combine(fixtures, "update-0.9.0.sh"),
                    Path.Combine(Root, ".mauve-governance/update.sh"));
                SetInstalledVersion(installedVersion);
            }
            File.WriteAllText(Path.Combine(Root, "consumer.txt"), "consumer-owned");
            client = new HttpClient(new FixtureHandler(this));
            Updater = new ReleaseUpdater(client, RunAsync);
        }

        public void SetInstalledVersion(string version) =>
            File.WriteAllText(Path.Combine(Root, ".config/dotnet-tools.json"),
                JsonSerializer.Serialize(new { tools = new Dictionary<string, object>
                {
                    ["mauve.governance.tool"] = new { version }
                } }));

        private Task<string> RunAsync(string file, IReadOnlyList<string> args,
            IReadOnlyDictionary<string, string>? environment, CancellationToken token)
        {
            Calls.Add(file.StartsWith(Root, StringComparison.Ordinal)
                ? "governance " + string.Join(" ", args)
                : file + " " + string.Join(" ", args));
            if (file == "gh" && args[0] == "release")
            {
                return Task.FromResult("v" + ReleaseVersion + "\n");
            }
            if (file == "gh" && args[0] == "api")
            {
                var assets = Packages.Select((name, index) => new
                {
                    name = $"Mauve.Governance.{name}.{ReleaseVersion}.nupkg",
                    digest = MissingDigest && index == 0 ? null :
                        "sha256:" + Convert.ToHexString(SHA256.HashData(package)),
                    size = OversizedAsset && index == 0 ? 129L * 1024 * 1024 : package.Length,
                    id = index + 1
                });
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    tag_name = WrongTag ? "v0.9.0" : "v" + ReleaseVersion,
                    draft = false,
                    prerelease = false,
                    assets
                }));
            }
            if (file == "dotnet")
            {
                Assert.Equal("Mauve.Governance.Tool", args[2]);
                Assert.Equal(ReleaseVersion, args[4]);
                Assert.NotNull(environment);
                Assert.StartsWith(Root, environment!["NUGET_PACKAGES"], StringComparison.Ordinal);
                var directory = args[6];
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, OperatingSystem.IsWindows() ? "governance.exe" : "governance"), "");
                var config = File.ReadAllText(args[8]);
                Assert.Contains("Mauve.Governance.*", config);
                Assert.Contains("nuget.org/v3/index.json", config);
            }
            else if (file.StartsWith(Root, StringComparison.Ordinal) && args[0] == "update" && ApplyUpdate)
            {
                if (!File.Exists(Path.Combine(Root, "docs/validation-gaps.md")))
                {
                    File.WriteAllText(Path.Combine(Root, "docs/validation-gaps.md"), "New editable ledger");
                }
                File.Delete(Path.Combine(Root, ".mauve-governance/update.sh"));
                File.WriteAllText(Path.Combine(Root, ".mauve-governance/manifest.json"),
                    JsonSerializer.Serialize(new { BundleVersion = ReleaseVersion,
                        Files = new[] { new { Path = "docs/governance/new.md" } } }));
                SetInstalledVersion(ReleaseVersion);
                Directory.CreateDirectory(Path.Combine(Root, "docs/governance"));
                File.WriteAllText(Path.Combine(Root, "docs/governance/new.md"), "new");
            }
            else if (file == "git" && args[2] == "diff")
            {
                if (Calls.Count(call => call.StartsWith("git ", StringComparison.Ordinal) && call.Contains(" diff ", StringComparison.Ordinal)) == 1)
                {
                    return Task.FromResult("");
                }
                return Task.FromResult(UnmanagedStaged
                    ? "docs/governance/new.md\0consumer.txt\0"
                    : "docs/governance/new.md\0.mauve-governance/update.sh\0.mauve-governance/manifest.json\0");
            }
            return Task.FromResult("");
        }

        public void Dispose()
        {
            client.Dispose();
            Directory.Delete(Root, recursive: true);
        }

        private sealed class FixtureHandler(ReleaseFixture fixture) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Assert.StartsWith("https://api.github.com/repos/mauve/Mauve.Governance/releases/assets/",
                    request.RequestUri!.ToString(), StringComparison.Ordinal);
                Assert.Contains(request.Headers.Accept, header => header.MediaType == "application/octet-stream");
                fixture.DownloadedAssets.Add(request.RequestUri.ToString());
                var bytes = fixture.Tampered ? Encoding.UTF8.GetBytes("tampered package") : fixture.package;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(bytes)
                });
            }
        }
    }
}
