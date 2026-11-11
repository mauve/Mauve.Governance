using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mauve.Governance.TestResults;

if (args.Length != 4)
{
    throw new ArgumentException("Expected one runner-generated TRX per framework.");
}

var report = args.Aggregate(TrxEvidence.Empty, TrxEvidence.AddTrx);
if (report.Runs.Length != 4 ||
    report.Runs.Select(run => run.Tests.Length).Order().SequenceEqual([2, 3, 3, 3]) is false ||
    report.Features is not [{ Id: "FEAT-Payments", Passed: 11 }] ||
    report.Requirements.Select(requirement => requirement.Id).Order().SequenceEqual(
        ["FEAT-Payments-01", "FEAT-Payments-02",
         "FEAT-Payments-03", "FEAT-Payments-04"]) is false)
{
    throw new InvalidDataException("Runner TRX metadata was incomplete or invalid.");
}

foreach (var (index, framework) in new[] { "MSTest", "XunitV2", "NUnit", "XunitV3" }.Index())
{
    if (!args[index].Contains("/" + framework + "/", StringComparison.Ordinal))
    {
        throw new InvalidDataException($"Expected a {framework} runner TRX at position {index}.");
    }

    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[index])));
    var run = report.Runs.Single(item => item.Sha256 == hash);
    if (run.Tests.Length != (index == 0 ? 2 : 3))
    {
        throw new InvalidDataException($"Incomplete {framework} runner results.");
    }
    Check(run, "NativeMetadataIsDiscoverable", ["FEAT-Payments-01"]);
    Check(run, "ParameterizedMetadataIsDiscoverable", ["FEAT-Payments-02"]);
    if (index != 0)
    {
        Check(run, "ConvenienceMetadataIsDiscoverable",
            ["FEAT-Payments-03", "FEAT-Payments-04"]);
    }
}

Console.WriteLine("Verified 11 real runner results with native and convenience metadata in four TRX runs.");

static void Check(ImportedRun run, string name, string[] requirements)
{
    var test = run.Tests.Single(test => test.Name.Contains(name, StringComparison.Ordinal));
    if (test.Feature != "FEAT-Payments" || test.Outcome != "Passed" ||
        !test.Requirements.SequenceEqual(requirements))
    {
        throw new InvalidDataException($"Incomplete runner metadata for {name}.");
    }
}
