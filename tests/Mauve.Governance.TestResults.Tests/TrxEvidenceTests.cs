using System.Security;
using System.Text;

namespace Mauve.Governance.TestResults.Tests;

public sealed class TrxEvidenceTests
{
    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Merge_is_additive_order_independent_and_deduplicates_identical_trx()
    {
        using var fixtures = new Fixtures();
        var first = fixtures.Write("first.trx", Trx("scenario-1",
            ("a", "FEAT-01", "Passed", "FEAT-01-001"),
            ("b", "FEAT-02", "Passed", "FEAT-02-001"),
            ("c", "FEAT-03", "Skipped", "FEAT-03-001")));
        var second = fixtures.Write("second.trx", Trx("scenario-2",
            ("a", "FEAT-01", "Skipped", "FEAT-01-001"),
            ("b", "FEAT-02", "Skipped", "FEAT-02-001"),
            ("c", "FEAT-03", "Failed", "FEAT-03-001")));
        var forward = TrxEvidence.AddTrx(TrxEvidence.AddTrx(TrxEvidence.Empty, first), second);
        var reverse = TrxEvidence.AddTrx(TrxEvidence.AddTrx(TrxEvidence.Empty, second), first);

        Assert.Equal(new[] { "Passed", "Passed", "Failed" },
            forward.Features.Select(static feature => feature.Status));
        Assert.Equal(forward, TrxEvidence.AddTrx(forward, first));
        Assert.Equal(forward.Features, reverse.Features);
        Assert.Equal("Failed", Assert.Single(forward.Requirements,
            static item => item.Id == "FEAT-03-001").Status);

        var report = Path.Combine(fixtures.Root, "report.json");
        TrxEvidence.Save(forward, report);
        Assert.Equal(forward.Features, TrxEvidence.LoadReport(report).Features);
        Assert.Equal(2, TrxEvidence.LoadReport(report).Runs.Length);
        var third = fixtures.Write("third.trx", Trx("scenario-3",
            ("c", "FEAT-03", "Passed", "FEAT-03-001")));
        var extended = TrxEvidence.AddTrx(TrxEvidence.LoadReport(report), third);
        Assert.Equal("Mixed", Assert.Single(extended.Features,
            static item => item.Id == "FEAT-03").Status);
        TrxEvidence.Save(extended, report);
        Assert.Equal(3, TrxEvidence.LoadReport(report).Runs.Length);
    }

    [Theory]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-002")]
    [InlineData("Passed", "Failed", "Mixed")]
    [InlineData("Passed", "Timeout", "Indeterminate")]
    [InlineData("Failed", "Timeout", "Failed")]
    [InlineData("Skipped", "NotExecuted", "NotObserved")]
    [InlineData("Passed", "Skipped", "Passed")]
    public void Merge_preserves_outcome_flags(string firstOutcome, string secondOutcome, string expected)
    {
        using var fixtures = new Fixtures();
        var first = fixtures.Write("first.trx", Trx("one", ("a", "FEAT-01", firstOutcome, "FEAT-01-001")));
        var second = fixtures.Write("second.trx", Trx("two", ("a", "FEAT-01", secondOutcome, "FEAT-01-001")));
        var merged = TrxEvidence.AddTrx(TrxEvidence.AddTrx(TrxEvidence.Empty, first), second);

        Assert.Equal(expected, Assert.Single(merged.Features).Status);
        Assert.Equal(expected, Assert.Single(merged.Requirements).Status);
        Assert.Equal(expected, Assert.Single(merged.Tests).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-002")]
    public void Indeterminate_keeps_prior_pass_for_later_failure()
    {
        using var fixtures = new Fixtures();
        var report = TrxEvidence.Empty;
        foreach (var (index, outcome) in new[] { "Passed", "Timeout", "Failed" }.Index())
        {
            var file = fixtures.Write($"{index}.trx", Trx($"run-{index}",
                ("a", "FEAT-01", outcome, "FEAT-01-001")));
            report = TrxEvidence.AddTrx(report, file);
        }

        Assert.Equal("Mixed", Assert.Single(report.Features).Status);
        Assert.Equal(1, Assert.Single(report.Features).Other);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Allows_consumer_selected_feature_identifiers()
    {
        using var fixtures = new Fixtures();
        var file = fixtures.Write("single-digit.trx", Trx("one",
            ("a", "FEAT-1", "Passed", "FEAT-1-001")));
        Assert.Equal("FEAT-1", Assert.Single(TrxEvidence.AddTrx(TrxEvidence.Empty, file).Features).Id);
        var named = fixtures.Write("named.trx", Trx("two",
            ("a", "FEAT-Checkout", "Passed", "FEAT-Checkout-001")));
        Assert.Equal("FEAT-Checkout",
            Assert.Single(TrxEvidence.AddTrx(TrxEvidence.Empty, named).Features).Id);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Rejects_missing_metadata_and_external_entities()
    {
        using var fixtures = new Fixtures();
        var missing = fixtures.Write("missing.trx",
            "<TestRun id=\"one\"><TestDefinitions><UnitTest id=\"a\" name=\"a\"/></TestDefinitions>" +
            "<Results><UnitTestResult testId=\"a\" testName=\"a\" outcome=\"Passed\"/></Results></TestRun>");
        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, missing));

        var hostile = fixtures.Write("hostile.trx",
            "<!DOCTYPE TestRun [<!ENTITY x SYSTEM \"file:///etc/passwd\">]>" +
            "<TestRun id=\"&x;\"/>");
        Assert.Throws<System.Xml.XmlException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, hostile));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Reads_schema_property_key_and_value_without_accepting_missing_feature()
    {
        using var fixtures = new Fixtures();
        const string prefix = "<TestRun id=\"schema\"><TestDefinitions><UnitTest id=\"a\" name=\"a\"><Properties>";
        const string suffix = "</Properties></UnitTest></TestDefinitions><Results>" +
            "<UnitTestResult testId=\"a\" testName=\"a\" outcome=\"Passed\"/></Results></TestRun>";
        var valid = fixtures.Write("schema.trx", prefix +
            "<Property><Key>Feature</Key><Value>FEAT-01</Value></Property>" +
            "<Property><Key>Requirement</Key><Value>FEAT-01-001</Value></Property>" + suffix);
        Assert.Equal("FEAT-01", Assert.Single(
            TrxEvidence.AddTrx(TrxEvidence.Empty, valid).Features).Id);

        var missing = fixtures.Write("missing-schema.trx", prefix +
            "<Property><Key>Requirement</Key><Value>FEAT-01-001</Value></Property>" + suffix);
        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, missing));
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Rejects_report_with_inconsistent_aggregates()
    {
        using var fixtures = new Fixtures();
        var trx = fixtures.Write("first.trx", Trx("run", ("a", "FEAT-01", "Passed", "FEAT-01-001")));
        var path = Path.Combine(fixtures.Root, "report.json");
        TrxEvidence.Save(TrxEvidence.AddTrx(TrxEvidence.Empty, trx), path);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"Passed\"", "\"Failed\"", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => TrxEvidence.LoadReport(path));
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-002")]
    public void Rejects_same_run_id_with_conflicting_content()
    {
        using var fixtures = new Fixtures();
        var first = fixtures.Write("first.trx", Trx("same-run",
            ("a", "FEAT-01", "Passed", "FEAT-01-001")));
        var second = fixtures.Write("second.trx", Trx("same-run",
            ("a", "FEAT-01", "Failed", "FEAT-01-001")));
        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, first);
        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(report, second));
    }

    private static string Trx(
        string run,
        params (string Id, string Feature, string Outcome, string Requirement)[] tests)
    {
        var xml = new StringBuilder($"<TestRun id=\"{run}\"><TestDefinitions>");
        foreach (var test in tests)
        {
            xml.Append($"<UnitTest id=\"{test.Id}\" name=\"{test.Id}\"><Properties>")
                .Append($"<Property Name=\"Feature\" Value=\"{test.Feature}\"/>")
                .Append($"<Property Name=\"Requirement\" Value=\"{test.Requirement}\"/>")
                .Append("</Properties></UnitTest>");
        }

        xml.Append("</TestDefinitions><Results>");
        foreach (var test in tests)
        {
            xml.Append($"<UnitTestResult testId=\"{test.Id}\" testName=\"{test.Id}\" outcome=\"{SecurityElement.Escape(test.Outcome)}\"/>");
        }

        return xml.Append("</Results></TestRun>").ToString();
    }

    private sealed class Fixtures : IDisposable
    {
        public string Root { get; } = Path.Combine(Directory.GetCurrentDirectory(),
            "artifacts", $"governance-testresults-{Guid.NewGuid():N}");

        public Fixtures() => Directory.CreateDirectory(Root);

        public string Write(string name, string content)
        {
            var path = Path.Combine(Root, name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
