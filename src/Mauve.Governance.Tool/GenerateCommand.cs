using System.CommandLine;
using Mauve.Governance.Traceability;

namespace Mauve.Governance.Tool;

internal static class GenerateCommand
{
    public static Command Create()
    {
        var root = new Option<string>("--root")
        {
            Description = "Source directory to scan for test metadata.",
            Required = true
        };
        var output = new Option<string>("--output")
        {
            Description = "Path to the generated source-only JSON index.",
            Required = true
        };
        var check = new Option<bool>("--check")
        {
            Description = "Compare with the existing index without writing."
        };
        var command = new Command("generate", "Produce source-only test traceability.");
        command.Options.Add(root);
        command.Options.Add(output);
        command.Options.Add(check);
        command.SetAction(parse =>
        {
            var path = parse.GetValue(root);
            var outputFile = parse.GetValue(output);
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(outputFile) ||
                string.Equals(Path.GetExtension(outputFile), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return CommandErrors.Usage("Require a source root and JSON output path, not a C# source file.");
            }
            return CommandErrors.Execute(() => Run(path, outputFile, parse.GetValue(check)));
        });
        return command;
    }

    private static int Run(string root, string outputFile, bool check)
    {
        var report = TraceabilityGenerator.GenerateFromDirectory(root);
        foreach (var diagnostic in report.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Path}:{diagnostic.Line}: {diagnostic.Message}");
        }
        if (!report.IsValid)
        {
            return 1;
        }
        var json = report.ToJson();
        if (check)
        {
            if (!File.Exists(outputFile) || new FileInfo(outputFile).Length > 64 * 1024 * 1024 ||
                File.ReadAllText(outputFile) != json)
            {
                Console.Error.WriteLine("MG-GOV-0002: Source-only traceability output is missing or stale.");
                return 1;
            }
        }
        else
        {
            var path = Path.GetFullPath(outputFile);
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $".governance-trace-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, json);
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        Console.WriteLine($"Source-only traceability: {report.Tests.Count} tests, {report.Features.Count} features.");
        return 0;
    }
}
