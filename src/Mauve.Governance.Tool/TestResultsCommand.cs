using System.CommandLine;
using Mauve.Governance.TestResults;

namespace Mauve.Governance.Tool;

internal static class TestResultsCommand
{
    public static Command Create()
    {
        var trx = new Option<string[]>("--trx")
        {
            Description = "TRX run file to import; repeat for additional runs.",
            Arity = ArgumentArity.OneOrMore
        };
        var input = new Option<string>("--input-report")
        {
            Description = "Existing evidence report to extend."
        };
        var output = new Option<string>("--output")
        {
            Description = "Path for the versioned JSON evidence report.",
            Required = true
        };
        var command = new Command("test-results", "Import TRX run evidence into a versioned report.");
        command.Options.Add(trx);
        command.Options.Add(input);
        command.Options.Add(output);
        command.SetAction(parse =>
        {
            var files = parse.GetValue(trx);
            var outputFile = parse.GetValue(output);
            if (files is not { Length: > 0 } || string.IsNullOrWhiteSpace(outputFile) ||
                files.Any(string.IsNullOrWhiteSpace))
            {
                return CommandErrors.Usage("At least one TRX file and --output are required.");
            }
            return CommandErrors.Execute(() => Run(files, parse.GetValue(input), outputFile));
        });
        return command;
    }

    private static int Run(string[] files, string? input, string output)
    {
        if (files.Any(file => string.Equals(Path.GetFullPath(file), Path.GetFullPath(output), StringComparison.Ordinal)))
        {
            return CommandErrors.Usage("Report output cannot replace a TRX input.");
        }
        var report = input is null ? TrxEvidence.Empty : TrxEvidence.LoadReport(input);
        foreach (var file in files)
        {
            report = TrxEvidence.AddTrx(report, file);
        }
        TrxEvidence.Save(report, output);
        Console.WriteLine($"Imported {report.Runs.Length} distinct TRX runs; {report.Features.Length} features:");
        foreach (var feature in report.Features)
        {
            Console.WriteLine($"{feature.Id}: {feature.Status} (pass={feature.Passed}, fail={feature.Failed}, other={feature.Other}, skipped={feature.Skipped})");
        }
        return 0;
    }
}
