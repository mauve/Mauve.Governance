using System.Diagnostics;
using System.CommandLine;
using Mauve.Governance.Tool;

namespace Mauve.Governance.TestResults.Tests;

public sealed class CliProcessTests
{
    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Help_and_invalid_arguments_have_stable_exit_status()
    {
        var help = Run("--help");
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("init", help.Output);
        Assert.Contains("update-release", help.Output);

        var missingRoot = Run("init", "--profile", "core",
            "--bundle-version", RepositorySetup.CurrentBundleVersion);
        Assert.Equal(2, missingRoot.ExitCode);
        Assert.Contains("MG-GOV-0001", missingRoot.Error);

        var incompatible = Run("update", "--root", ".", "--apply", "--check");
        Assert.Equal(2, incompatible.ExitCode);
        Assert.Contains("MG-GOV-0001", incompatible.Error);

        var unknown = Run("not-a-command");
        Assert.Equal(2, unknown.ExitCode);
        Assert.Contains("MG-GOV-0001", unknown.Error);
    }

    [Fact]
    [Trait("Feature", "FEAT-02")]
    [Trait("Requirement", "FEAT-02-001")]
    public void Every_command_option_is_documented_in_help()
    {
        Command[] commands =
        [
            InitCommand.Create(),
            UpdateCommand.Create(),
            ValidateCommand.Create(),
            DoctorCommand.Create(),
            GenerateCommand.Create(),
            TestResultsCommand.Create(),
            UpdateReleaseCommand.Create()
        ];
        foreach (var command in commands)
        {
            var help = Run(command.Name, "--help");
            Assert.Equal(0, help.ExitCode);
            var text = string.Join(' ', help.Output.Split((char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
            foreach (var option in command.Options)
            {
                Assert.False(string.IsNullOrWhiteSpace(option.Description),
                    $"{command.Name} {option.Name} lacks documentation.");
                Assert.Contains(option.Name, help.Output);
                Assert.Contains(option.Description, text);
            }
        }
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-002")]
    public void Generate_check_ignores_comment_only_edits_but_detects_metadata_changes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"governance-generate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var source = Path.Combine(directory, "test.cs");
            var index = Path.Combine(directory, "index.json");
            const string test = """
                using Xunit;
                class C {
                    [Fact] [Trait("Feature", "FEAT-1")]
                    [Trait("Requirement", "FEAT-1-A")]
                    void Works() {}
                }
                """;
            File.WriteAllText(source, test);
            Assert.Equal(0, Run("generate", "--root", directory, "--output", index).ExitCode);
            var original = File.ReadAllText(index);

            File.WriteAllText(source, "// Notes about this file.\n\n" + test);
            Assert.Equal(0, Run("generate", "--root", directory, "--output", index, "--check").ExitCode);
            Assert.Equal(original, File.ReadAllText(index));

            File.WriteAllText(source, test.Replace("FEAT-1-A", "FEAT-1-B", StringComparison.Ordinal));
            var stale = Run("generate", "--root", directory, "--output", index, "--check");
            Assert.Equal(1, stale.ExitCode);
            Assert.Contains("MG-GOV-0002", stale.Error);
            Assert.Equal(original, File.ReadAllText(index));

            Assert.Equal(0, Run("generate", "--root", directory, "--output", index).ExitCode);
            Assert.Equal(0, Run("generate", "--root", directory, "--output", index, "--check").ExitCode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-002")]
    public void Repeated_trx_options_import_separate_runs()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"governance-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var first = Path.Combine(directory, "first.trx");
            var second = Path.Combine(directory, "second.trx");
            var report = Path.Combine(directory, "report.json");
            File.WriteAllText(first, Trx("first"));
            File.WriteAllText(second, Trx("second"));

            var result = Run("test-results", "--trx", first, "--trx", second,
                "--output", report);
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Imported 2 distinct TRX runs", result.Output);
            Assert.True(File.Exists(report));

            var collision = Run("test-results", "--trx", first, "--output", first);
            Assert.Equal(2, collision.ExitCode);
            Assert.Contains("MG-GOV-0001", collision.Error);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Trx(string id) =>
        $"<TestRun id=\"{id}\"><TestDefinitions><UnitTest id=\"test\" name=\"test\"><Properties>" +
        "<Property Name=\"Feature\" Value=\"FEAT-01\"/>" +
        "<Property Name=\"Requirement\" Value=\"FEAT-01-002\"/>" +
        "</Properties></UnitTest></TestDefinitions><Results>" +
        "<UnitTestResult testId=\"test\" testName=\"test\" outcome=\"Passed\"/></Results></TestRun>";

    private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
    {
        var assembly = typeof(RepositorySetup).Assembly.Location;
        var testAssembly = typeof(CliProcessTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--runtimeconfig");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".runtimeconfig.json"));
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(Path.ChangeExtension(testAssembly, ".deps.json"));
        start.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(30_000), "Governance CLI did not exit within 30 seconds.");
        return (process.ExitCode, output, error);
    }
}
