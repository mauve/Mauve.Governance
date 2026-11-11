using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Mauve.Governance.Tool;

internal sealed class ReleaseUpdater
{
    private const string Repository = "mauve/Mauve.Governance";
    private const string Manifest = ".mauve-governance/manifest.json";
    private const long MaxAssetBytes = 128 * 1024 * 1024;
    private const int MaxMetadataBytes = 1024 * 1024;
    private static readonly string[] Packages = ["Tool", "TestResults", "Traceability", "Requirements"];
    private readonly HttpClient client;
    private readonly Func<string, IReadOnlyList<string>, IReadOnlyDictionary<string, string>?, CancellationToken, Task<string>> run;

    internal ReleaseUpdater(HttpClient client,
        Func<string, IReadOnlyList<string>, IReadOnlyDictionary<string, string>?, CancellationToken, Task<string>> run)
    {
        this.client = client;
        this.run = run;
    }

    internal static int Run(string root)
    {
        var outputPath = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidDataException("GITHUB_OUTPUT is required for update-release.");
        }
        try
        {
            RunAsync(root, outputPath).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
            InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"Release preparation failed: {exception.Message}", exception);
        }
        return 0;
    }

    internal static async Task<bool> RunAsync(string root, string outputPath, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        return await new ReleaseUpdater(client, RunProcessAsync).ApplyAsync(root, outputPath, cancellationToken);
    }

    internal async Task<bool> ApplyAsync(string root, string outputPath, CancellationToken cancellationToken = default)
    {
        var fullRoot = Path.GetFullPath(root);
        EnsureSafePath(fullRoot, ".config/dotnet-tools.json");
        var installed = ReadInstalledVersion(Path.Combine(fullRoot, ".config/dotnet-tools.json"));
        EnsureSafePath(fullRoot, Manifest);
        if (ReadBundleVersion(Path.Combine(fullRoot, Manifest)) != installed.ToString())
        {
            throw new InvalidDataException("Pinned Governance tool version does not match the ownership manifest.");
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(10));
        var token = deadline.Token;
        var tag = (await run("gh", ["release", "view", "--repo", Repository, "--json", "tagName", "--jq", ".tagName"],
            null, token)).Trim();
        if (!Regex.IsMatch(tag, @"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant) ||
            !Version.TryParse(tag[1..], out var released) || released.Build < 0 || released.Revision >= 0)
        {
            throw new InvalidDataException($"Latest Governance release has an unsupported tag: {tag}");
        }

        if (released < installed)
        {
            throw new InvalidDataException($"Release {released} would downgrade installed bundle {installed}.");
        }
        if (released == installed)
        {
            await AppendOutputAsync(outputPath, "changed=false\n", token);
            return false;
        }

        // Never trust a caller-controlled release repository or a URL supplied by release metadata.
        var version = released.ToString();
        var releaseJson = await run("gh", ["api", $"repos/{Repository}/releases/tags/{tag}"], null, token);
        if (Encoding.UTF8.GetByteCount(releaseJson) > MaxMetadataBytes)
        {
            throw new InvalidDataException("Release metadata exceeds the allowed size.");
        }
        var assets = ParseAssets(releaseJson, version);
        var existingStaged = await run("git", ["-C", fullRoot, "diff", "--cached", "--name-only", "-z"], null, token);
        if (existingStaged.Length != 0)
        {
            throw new InvalidDataException("Pre-existing staged changes block release preparation.");
        }
        var preflight = RepositorySetup.Run("update", fullRoot, check: true);
        if (preflight is not (0 or 1))
        {
            throw new InvalidDataException("Existing Governance ownership validation failed.");
        }
        var oldPaths = ReadOwnedPaths(Path.Combine(fullRoot, Manifest));
        var newScaffolds = RepositorySetup.EditableScaffoldPaths
            .Where(path => !File.Exists(Path.Combine(fullRoot, path))).ToArray();
        foreach (var path in oldPaths)
        {
            EnsureSafePath(fullRoot, path);
        }
        foreach (var path in newScaffolds)
        {
            EnsureSafePath(fullRoot, path);
        }
        var parent = Path.Combine(fullRoot, ".mauve-governance");
        EnsureSafeDirectory(fullRoot, parent);
        var work = Path.Combine(parent, $".release-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            var feed = Path.Combine(work, "feed");
            Directory.CreateDirectory(feed);
            foreach (var asset in assets)
            {
                await DownloadAsync(asset, Path.Combine(feed, asset.Name), token);
            }
            var config = Path.Combine(work, "NuGet.Config");
            new XDocument(new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "governance-release"), new XAttribute("value", feed)),
                    new XElement("add", new XAttribute("key", "nuget-org"),
                        new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
                new XElement("packageSourceMapping",
                    new XElement("packageSource", new XAttribute("key", "governance-release"),
                        new XElement("package", new XAttribute("pattern", "Mauve.Governance.*"))),
                    new XElement("packageSource", new XAttribute("key", "nuget-org"),
                        new XElement("package", new XAttribute("pattern", "*"))))))
                .Save(config);
            var tool = Path.Combine(work, "tool");
            var environment = new Dictionary<string, string>
            {
                ["NUGET_PACKAGES"] = Path.Combine(work, "packages")
            };
            await run("dotnet", ["tool", "install", "Mauve.Governance.Tool", "--version", version,
                "--tool-path", tool, "--configfile", config], environment, token);
            var executable = Path.Combine(tool, OperatingSystem.IsWindows() ? "governance.exe" : "governance");
            if (!File.Exists(executable))
            {
                throw new InvalidDataException("Verified Governance tool installation did not produce an executable.");
            }
            await run(executable, ["update", "--root", fullRoot, "--apply"], null, token);
            await run(executable, ["validate", "--root", fullRoot], null, token);
            if (ReadInstalledVersion(Path.Combine(fullRoot, ".config/dotnet-tools.json")) != released ||
                ReadBundleVersion(Path.Combine(fullRoot, Manifest)) != version)
            {
                throw new InvalidDataException("Updated manifest and pinned tool must match the verified release.");
            }
            var newPaths = ReadOwnedPaths(Path.Combine(fullRoot, Manifest));
            foreach (var path in oldPaths.Concat(newPaths).Concat(newScaffolds).Append(Manifest)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                EnsureSafePath(fullRoot, path);
                await run("git", ["-C", fullRoot, "add", "-A", "--", path], null, token);
            }
            var status = await run("git", ["-C", fullRoot, "diff", "--cached", "--name-only", "-z"], null, token);
            var allowed = oldPaths.Concat(newPaths).Concat(newScaffolds).Append(Manifest)
                .ToHashSet(StringComparer.Ordinal);
            var staged = status.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            if (staged.Any(path => !allowed.Contains(path)))
            {
                throw new InvalidDataException("Unmanaged changes are staged; refusing to prepare an update.");
            }
            var changed = staged.Length != 0;
            await AppendOutputAsync(outputPath, changed ? $"version={version}\nchanged=true\n" : "changed=false\n", token);
            return changed;
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private static Version ReadInstalledVersion(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var value = Required(Required(Required(document.RootElement, "tools"), "mauve.governance.tool"),
            "version").GetString();
        if (value is null || !Regex.IsMatch(value, @"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") ||
            !Version.TryParse(value, out var version) || version.Build < 0 || version.Revision >= 0)
        {
            throw new InvalidDataException("Installed Governance tool version is not a three-part release version.");
        }
        return version;
    }

    private static IReadOnlyList<ReleaseAsset> ParseAssets(string json, string version)
    {
        using var document = JsonDocument.Parse(json);
        if (Required(document.RootElement, "tag_name").GetString() != $"v{version}" ||
            Required(document.RootElement, "draft").GetBoolean() ||
            Required(document.RootElement, "prerelease").GetBoolean())
        {
            throw new InvalidDataException("Release metadata does not describe the trusted published tag.");
        }
        var all = Required(document.RootElement, "assets").EnumerateArray().ToArray();
        var assets = new List<ReleaseAsset>();
        foreach (var package in Packages)
        {
            var name = $"Mauve.Governance.{package}.{version}.nupkg";
            var matches = all.Where(item => Required(item, "name").GetString() == name).ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException($"Expected exactly one release asset for {name}.");
            }
            var asset = matches[0];
            var digest = Required(asset, "digest").GetString();
            if (digest is null || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$"))
            {
                throw new InvalidDataException($"Missing trusted SHA-256 digest for {name}.");
            }
            if (!Required(asset, "size").TryGetInt64(out var size) ||
                !Required(asset, "id").TryGetInt64(out var id))
            {
                throw new InvalidDataException($"Invalid release asset size or id for {name}.");
            }
            if (size is < 1 or > MaxAssetBytes || id <= 0)
            {
                throw new InvalidDataException($"Invalid release asset size or id for {name}.");
            }
            assets.Add(new ReleaseAsset(name, digest[7..], size, id));
        }
        return assets;
    }

    private async Task DownloadAsync(ReleaseAsset asset, string path, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Repository}/releases/assets/{asset.Id}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        request.Headers.UserAgent.ParseAdd("Mauve.Governance.Tool");
        var githubToken = Environment.GetEnvironmentVariable("GH_TOKEN");
        if (!string.IsNullOrEmpty(githubToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken);
        }
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxAssetBytes or < 1)
        {
            throw new InvalidDataException($"Release asset exceeds size bound: {asset.Name}.");
        }
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long length = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            length += count;
            if (length > MaxAssetBytes || length > asset.Size)
            {
                throw new InvalidDataException($"Release asset exceeds size bound: {asset.Name}.");
            }
            hasher.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        if (length != asset.Size || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(asset.Digest), hasher.GetHashAndReset()))
        {
            throw new InvalidDataException($"Release asset digest mismatch for {asset.Name}.");
        }
    }

    private static HashSet<string> ReadOwnedPaths(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Required(document.RootElement, "Files").EnumerateArray()
            .Select(item => Required(item, "Path").GetString() ??
                throw new InvalidDataException("Manifest contains a null owned path."))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? ReadBundleVersion(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Required(document.RootElement, "BundleVersion").GetString();
    }

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) ? property :
            throw new InvalidDataException($"Missing required release or manifest field: {name}.");

    private static void EnsureSafeDirectory(string root, string directory)
    {
        if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Governance workspace must be a real directory.");
        }
    }

    private static void EnsureSafePath(string root, string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith('/') || path.Contains('\\') ||
            path.Split('/').Any(part => part is "" or "." or ".." or ".git") ||
            path.Any(char.IsControl))
        {
            throw new InvalidDataException($"Unsafe manifest-owned path: {path}");
        }
        var current = root;
        foreach (var part in path.Split('/'))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"Symlink in manifest-owned path: {path}");
            }
        }
    }

    private static async Task AppendOutputAsync(string path, string text, CancellationToken token)
    {
        await File.AppendAllTextAsync(path, text, token);
    }

    internal static async Task<string> RunProcessAsync(string file, IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment, CancellationToken token)
    {
        var start = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                start.Environment[key] = value;
            }
        }
        Process started;
        try
        {
            started = Process.Start(start) ?? throw new IOException($"Could not start {file}.");
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new IOException($"Could not start {file}: {exception.Message}", exception);
        }
        using var process = started;
        try
        {
            var stdout = ReadBoundedAsync(process.StandardOutput, MaxMetadataBytes, token);
            var stderr = ReadBoundedAsync(process.StandardError, MaxMetadataBytes, token);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(token));
            var output = await stdout;
            var error = await stderr;
            if (process.ExitCode != 0)
            {
                throw new InvalidDataException($"{file} exited with {process.ExitCode}: {error}");
            }
            return output;
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer, token)) != 0)
        {
            if (result.Length + count > limit)
            {
                throw new InvalidDataException("Process output exceeds the allowed size.");
            }
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }

    private sealed record ReleaseAsset(string Name, string Digest, long Size, long Id);
}
