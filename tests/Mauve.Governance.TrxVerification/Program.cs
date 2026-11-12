using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using Mauve.Governance.TestResults;

if (args is ["--diagnostics", .. var diagnosticFiles])
{
    foreach (var file in diagnosticFiles)
    {
        VerifyDiagnostics(file);
    }
    Console.WriteLine("Verified real runner diagnostics and copied attachment bytes against official TRX.");
    return;
}

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
    VerifyDiagnostics(args[index]);
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

static void VerifyDiagnostics(string path)
{
    XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    var official = XDocument.Load(Path.Combine(Path.GetDirectoryName(path)!, "official.trx"));
    var governance = XDocument.Load(path);
    foreach (var result in governance.Descendants(ns + "UnitTestResult"))
    {
        var matches = official.Descendants(ns + "UnitTestResult").Where(candidate =>
            candidate.Attribute("testName")?.Value == result.Attribute("testName")?.Value).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException("Execution identity differs from official TRX.");
        }
        var reference = matches[0];
        foreach (var field in new[] { "StdOut", "StdErr", "DebugTrace", "StackTrace" })
        {
            var actual = result.Descendants(ns + field).SingleOrDefault()?.Value;
            var expected = reference.Descendants(ns + field).SingleOrDefault()?.Value;
            if ((actual ?? "").TrimEnd('\r', '\n') != (expected ?? "").TrimEnd('\r', '\n'))
            {
                throw new InvalidDataException($"Runner {field} differs from official TRX.");
            }
        }
        foreach (var field in new[] { "startTime", "endTime" })
        {
            if (reference.Attribute(field) is { } time &&
                DateTimeOffset.Parse(result.Attribute(field)!.Value) != DateTimeOffset.Parse(time.Value))
            {
                throw new InvalidDataException($"Runner {field} differs from official TRX.");
            }
        }
    }
    var companion = Path.Combine(Path.ChangeExtension(path, ".files"), "runner-data.xml");
    var raw = XDocument.Load(companion);
    foreach (var attachment in raw.Descendants("Attachment"))
    {
        var source = attachment.Element("ResolvedSource")!.Value;
        var copy = Path.Combine(Path.GetDirectoryName(path)!, attachment.Element("Path")!.Value);
        if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(copy)))
        {
            throw new InvalidDataException("Copied runner attachment differs from its original.");
        }
    }
    if (path.Contains("/MSTest/", StringComparison.Ordinal))
    {
        if (!governance.Descendants(ns + "StdOut").Any(output => output.Value.Contains("governance", StringComparison.Ordinal)) ||
            !governance.Descendants(ns + "StdErr").Any(output => output.Value.Contains("governance", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The real MSTest fixture must exercise both output channels.");
        }
        if (path.Contains("trx-metadata-check", StringComparison.Ordinal) &&
            !raw.Descendants("Attachment").Any())
        {
            throw new InvalidDataException("The real MSTest fixture must exercise attachment copying.");
        }
    }
}

static void Check(ImportedRun run, string name, string[] requirements)
{
    var test = run.Tests.Single(test => test.Name.Contains(name, StringComparison.Ordinal));
    if (test.Feature != "FEAT-Payments" || test.Outcome != "Passed" ||
        !test.Requirements.SequenceEqual(requirements))
    {
        throw new InvalidDataException($"Incomplete runner metadata for {name}.");
    }
}
