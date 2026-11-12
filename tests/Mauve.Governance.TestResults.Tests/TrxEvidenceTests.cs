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

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Accepts_results_before_definitions_and_keeps_execution_display_name()
    {
        using var fixtures = new Fixtures();
        var file = fixtures.Write("reordered.trx",
            "<TestRun id=\"reordered\"><Results>" +
            "<UnitTestResult testId=\"method\" testName=\"Method (row 1)\" outcome=\"Passed\"/>" +
            "</Results><TestDefinitions>" +
            Definition("method", "Method", "FEAT-01", "FEAT-01-001") +
            "</TestDefinitions></TestRun>");

        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, file);

        var observation = Assert.Single(report.Runs[0].Tests);
        Assert.Equal("Method (row 1)", observation.Name);
        Assert.Equal("FEAT-01", observation.Feature);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Rolls_up_data_driven_parents_without_erasing_unaccounted_failure()
    {
        using var fixtures = new Fixtures();
        var definitions = Definition("parent", "Parameterized", "FEAT-01", "FEAT-01-001") +
            Definition("child", "Parameterized row", "FEAT-01", "FEAT-01-001");
        var failedParent = Result("parent", "Parameterized", "Failed", "parent-exec",
            resultType: "DataDrivenTest");
        var failedChild = Result("child", "Parameterized row", "Failed", "child-exec",
            parentExecutionId: "parent-exec", resultType: "InnerDataDrivenResult");
        var accounted = fixtures.Write("accounted.trx",
            TrxWithSections("accounted", definitions, failedParent + failedChild));

        var accountedReport = TrxEvidence.AddTrx(TrxEvidence.Empty, accounted);

        Assert.Equal("child", Assert.Single(accountedReport.Runs[0].Tests).Id);
        Assert.Equal("Failed", Assert.Single(accountedReport.Features).Status);

        var passedChild = Result("child", "Parameterized row", "Passed", "child-exec",
            parentExecutionId: "parent-exec", resultType: "InnerDataDrivenResult");
        var unaccounted = fixtures.Write("unaccounted.trx",
            TrxWithSections("unaccounted", definitions, failedParent + passedChild));
        var unaccountedReport = TrxEvidence.AddTrx(TrxEvidence.Empty, unaccounted);

        Assert.Equal(2, unaccountedReport.Runs[0].Tests.Length);
        Assert.Equal("Mixed", Assert.Single(unaccountedReport.Features).Status);

        var noChildren = fixtures.Write("no-children.trx",
            TrxWithSections("no-children", definitions, failedParent));
        Assert.Single(TrxEvidence.AddTrx(TrxEvidence.Empty, noChildren).Runs[0].Tests);

        var differentRequirements = fixtures.Write("different-requirements.trx",
            TrxWithSections("different-requirements",
                Definition("parent", "Parameterized", "FEAT-01", "FEAT-01-001") +
                Definition("child", "Parameterized row", "FEAT-01", "FEAT-01-002"),
                failedParent + failedChild));
        var differentRequirementsReport = TrxEvidence.AddTrx(TrxEvidence.Empty, differentRequirements);
        Assert.Equal(2, differentRequirementsReport.Runs[0].Tests.Length);
        Assert.Equal("Failed", Assert.Single(differentRequirementsReport.Requirements,
            static item => item.Id == "FEAT-01-001").Status);

        var differentFeature = fixtures.Write("different-feature.trx",
            TrxWithSections("different-feature",
                Definition("parent", "Parameterized", "FEAT-01", "FEAT-01-001") +
                Definition("child", "Parameterized row", "FEAT-02", "FEAT-02-001"),
                failedParent + failedChild));
        var differentFeatureReport = TrxEvidence.AddTrx(TrxEvidence.Empty, differentFeature);
        Assert.Equal("Failed", Assert.Single(differentFeatureReport.Features,
            static item => item.Id == "FEAT-01").Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Infers_parent_from_nested_legacy_result_children()
    {
        using var fixtures = new Fixtures();
        var results = Result("parent", "Parameterized", "Passed", "parent-exec",
            resultType: "DataDrivenTest", innerResults:
                Result("child", "Parameterized row", "Failed", "child-exec",
                    resultType: "InnerDataDrivenResult"));
        var file = fixtures.Write("nested.trx", TrxWithSections("nested",
            Definition("parent", "Parameterized", "FEAT-01", "FEAT-01-001") +
            Definition("child", "Parameterized row", "FEAT-01", "FEAT-01-001"),
            results));

        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, file);

        Assert.Equal("child", Assert.Single(report.Runs[0].Tests).Id);
        Assert.Equal("Failed", Assert.Single(report.Features).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Rejects_ambiguous_or_cyclic_parent_execution_relationships()
    {
        using var fixtures = new Fixtures();
        var definitions = Definition("a", "a", "FEAT-01", "FEAT-01-001");
        var missing = fixtures.Write("missing-parent.trx", TrxWithSections("missing-parent", definitions,
            Result("a", "a", "Passed", "child", parentExecutionId: "absent")));
        var cycle = fixtures.Write("cycle.trx", TrxWithSections("cycle", definitions,
            Result("a", "a", "Passed", "first", parentExecutionId: "second") +
            Result("a", "a", "Passed", "second", parentExecutionId: "first")));
        var duplicate = fixtures.Write("duplicate-execution.trx", TrxWithSections("duplicate", definitions,
            Result("a", "a", "Passed", "same") + Result("a", "a", "Failed", "same")));

        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, missing));
        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, cycle));
        Assert.Throws<InvalidDataException>(() => TrxEvidence.AddTrx(TrxEvidence.Empty, duplicate));
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Ignores_result_and_metadata_elements_inside_unrelated_subtrees()
    {
        using var fixtures = new Fixtures();
        var result = "<UnitTestResult testId=\"a\" testName=\"a\" outcome=\"Failed\">" +
            "<Output>" + Result("a", "forged", "Passed") + "</Output></UnitTestResult>";
        var definition = "<UnitTest id=\"a\" name=\"a\"><Output>" +
            "<Property Name=\"Feature\" Value=\"FEAT-01\"/>" +
            "</Output></UnitTest>";
        var forgedMetadata = fixtures.Write("forged-metadata.trx",
            TrxWithSections("forged-metadata", definition, result));
        Assert.Throws<InvalidDataException>(() =>
            TrxEvidence.AddTrx(TrxEvidence.Empty, forgedMetadata));

        var ignoredResult = fixtures.Write("forged-result.trx",
            TrxWithSections("forged-result",
                Definition("a", "a", "FEAT-01", "FEAT-01-001"), result));
        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, ignoredResult);
        Assert.Equal("Failed", Assert.Single(report.Runs[0].Tests).Outcome);
        Assert.Equal("Failed", Assert.Single(report.Features).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Ignores_unrelated_traits_and_accepts_long_display_names()
    {
        using var fixtures = new Fixtures();
        var longName = new string('n', 5_000);
        var traits = new StringBuilder("<Properties>");
        for (var index = 0; index < 80; index++)
        {
            traits.Append("<Trait Name=\"Custom").Append(index)
                .Append("\" Value=\"").Append(new string('x', 5_000)).Append("\"/>");
        }

        traits.Append("<Trait Name=\"Feature\" Value=\"FEAT-01\"/>")
            .Append("<Trait Name=\"Requirement\" Value=\"FEAT-01-001\"/>")
            .Append("</Properties>");
        var file = fixtures.Write("many-traits.trx",
            "<TestRun id=\"traits\"><TestDefinitions><UnitTest id=\"a\" name=\"Definition\">" +
            traits + "</UnitTest></TestDefinitions><Results>" +
            Result("a", longName, "Passed") +
            "</Results></TestRun>");

        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, file);

        Assert.Equal(longName.Length, Assert.Single(report.Runs[0].Tests).Name.Length);
        Assert.Equal("Passed", Assert.Single(report.Features).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Streams_large_diagnostics_and_does_not_copy_them_into_the_report()
    {
        using var fixtures = new Fixtures();
        const int diagnosticCharacters = 33 * 1024 * 1024;
        var trx = fixtures.WriteLargeDiagnostics("large-diagnostics.trx", diagnosticCharacters);
        Assert.True(new FileInfo(trx).Length > 32L * 1024 * 1024);

        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, trx);
        var reportPath = Path.Combine(fixtures.Root, "report.json");
        TrxEvidence.Save(report, reportPath);

        Assert.Single(report.Runs[0].Tests);
        Assert.True(new FileInfo(reportPath).Length < 100_000);
        Assert.Equal("Passed", Assert.Single(TrxEvidence.LoadReport(reportPath).Features).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Saves_and_reloads_reports_beyond_the_old_observation_and_byte_limits()
    {
        using var fixtures = new Fixtures();
        const int observationCount = 100_001;
        var many = fixtures.WriteManyObservations("many.trx", observationCount);
        var report = TrxEvidence.AddTrx(TrxEvidence.Empty, many);
        Assert.Equal(observationCount, report.Runs[0].Tests.Length);

        var reportPath = Path.Combine(fixtures.Root, "many-report.json");
        TrxEvidence.Save(report, reportPath);
        Assert.Equal(observationCount, TrxEvidence.LoadReport(reportPath).Runs[0].Tests.Length);

        var large = fixtures.WriteLargeName("large-name.trx", 65 * 1024 * 1024);
        var largeReport = TrxEvidence.AddTrx(TrxEvidence.Empty, large);
        var largeReportPath = Path.Combine(fixtures.Root, "large-report.json");
        TrxEvidence.Save(largeReport, largeReportPath);

        Assert.True(new FileInfo(largeReportPath).Length > 64L * 1024 * 1024);
        Assert.Equal(65 * 1024 * 1024,
            TrxEvidence.LoadReport(largeReportPath).Runs[0].Tests[0].Name.Length);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Saves_reloads_and_extends_more_than_one_thousand_runs()
    {
        using var fixtures = new Fixtures();
        var report = TrxEvidence.Empty;
        for (var index = 0; index < 1001; index++)
        {
            var trx = fixtures.Write($"run-{index}.trx", Trx($"run-{index}",
                ("a", "FEAT-01", "Passed", "FEAT-01-001")));
            report = TrxEvidence.AddTrx(report, trx);
        }

        var reportPath = Path.Combine(fixtures.Root, "many-runs.json");
        TrxEvidence.Save(report, reportPath);
        var reloaded = TrxEvidence.LoadReport(reportPath);
        Assert.Equal(1001, reloaded.Runs.Length);
        Assert.Equal(1001, Assert.Single(reloaded.Features).Passed);

        var next = fixtures.Write("run-1001.trx",
            Trx("run-1001", ("a", "FEAT-01", "Failed", "FEAT-01-001")));
        var extended = TrxEvidence.AddTrx(reloaded, next);
        TrxEvidence.Save(extended, reportPath);
        var final = TrxEvidence.LoadReport(reportPath);
        Assert.Equal(1002, final.Runs.Length);
        Assert.Equal("Mixed", Assert.Single(final.Features).Status);
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Rejects_invalid_metadata_and_malformed_xml_after_output()
    {
        using var fixtures = new Fixtures();
        var invalidMetadata = fixtures.Write("invalid-metadata.trx",
            Trx("invalid", ("a", "FEAT-01", "Passed", "FEAT-02-001")));
        Assert.Throws<InvalidDataException>(() =>
            TrxEvidence.AddTrx(TrxEvidence.Empty, invalidMetadata));

        var malformed = fixtures.Write("malformed-output.trx",
            "<TestRun id=\"bad\"><Results><UnitTestResult testId=\"a\" testName=\"a\" outcome=\"Passed\">" +
            "<Output><StdOut>diagnostic</StdOut></Output></UnitTestResult></Results>" +
            Definition("a", "a", "FEAT-01", "FEAT-01-001") + "</TestRun> trailing");
        Assert.Throws<System.Xml.XmlException>(() =>
            TrxEvidence.AddTrx(TrxEvidence.Empty, malformed));

        var malformedDiagnostic = fixtures.Write("malformed-diagnostic.trx",
            "<TestRun id=\"bad-output\"><Results><UnitTestResult testId=\"a\" testName=\"a\" " +
            "outcome=\"Passed\"><Output><StdOut>diagnostic</Output></UnitTestResult></Results>" +
            "<TestDefinitions>" + Definition("a", "a", "FEAT-01", "FEAT-01-001") +
            "</TestDefinitions></TestRun>");
        Assert.Throws<System.Xml.XmlException>(() =>
            TrxEvidence.AddTrx(TrxEvidence.Empty, malformedDiagnostic));
    }

    [Fact]
    [Trait("Feature", "FEAT-01")]
    [Trait("Requirement", "FEAT-01-001")]
    public void Accepts_deep_irrelevant_output_and_definition_subtrees()
    {
        using var fixtures = new Fixtures();
        var openingTags = string.Concat(Enumerable.Repeat("<X>", 100));
        var closingTags = string.Concat(Enumerable.Repeat("</X>", 100));
        var file = fixtures.Write("deep-subtrees.trx",
            "<TestRun id=\"deep\"><TestDefinitions><UnitTest id=\"a\" name=\"a\">" +
            "<Properties><Property Name=\"Feature\" Value=\"FEAT-01\"/>" +
            "<Property Name=\"Requirement\" Value=\"FEAT-01-001\"/>" +
            "<Trait Name=\"Custom\" Value=\"ignored\">" + openingTags + "metadata" +
            closingTags + "</Trait></Properties></UnitTest></TestDefinitions><Results>" +
            "<UnitTestResult testId=\"a\" testName=\"a\" outcome=\"Passed\"><Output>" +
            openingTags + "diagnostic" + closingTags +
            "</Output></UnitTestResult></Results></TestRun>");

        Assert.Equal("Passed",
            Assert.Single(TrxEvidence.AddTrx(TrxEvidence.Empty, file).Features).Status);
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

    private static string Definition(string id, string name, string feature, string requirement) =>
        $"<UnitTest id=\"{SecurityElement.Escape(id)}\" name=\"{SecurityElement.Escape(name)}\">" +
        $"<Properties><Property Name=\"Feature\" Value=\"{SecurityElement.Escape(feature)}\"/>" +
        $"<Property Name=\"Requirement\" Value=\"{SecurityElement.Escape(requirement)}\"/></Properties></UnitTest>";

    private static string Result(
        string testId,
        string testName,
        string outcome,
        string? executionId = null,
        string? parentExecutionId = null,
        string? resultType = null,
        string? innerResults = null)
    {
        var attributes = new StringBuilder()
            .Append(" testId=\"").Append(SecurityElement.Escape(testId))
            .Append("\" testName=\"").Append(SecurityElement.Escape(testName))
            .Append("\" outcome=\"").Append(SecurityElement.Escape(outcome)).Append('"');
        if (executionId is not null)
        {
            attributes.Append(" executionId=\"").Append(SecurityElement.Escape(executionId)).Append('"');
        }
        if (parentExecutionId is not null)
        {
            attributes.Append(" parentExecutionId=\"")
                .Append(SecurityElement.Escape(parentExecutionId)).Append('"');
        }
        if (resultType is not null)
        {
            attributes.Append(" resultType=\"").Append(SecurityElement.Escape(resultType)).Append('"');
        }

        return innerResults is null
            ? "<UnitTestResult" + attributes + "/>"
            : "<UnitTestResult" + attributes + "><InnerResults>" + innerResults +
              "</InnerResults></UnitTestResult>";
    }

    private static string TrxWithSections(string run, string definitions, string results) =>
        $"<TestRun id=\"{SecurityElement.Escape(run)}\"><Results>{results}</Results>" +
        $"<TestDefinitions>{definitions}</TestDefinitions></TestRun>";

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

        public string WriteLargeDiagnostics(string name, int characterCount)
        {
            var path = Path.Combine(Root, name);
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false), 64 * 1024);
            writer.Write("<TestRun id=\"large\"><Results><UnitTestResult testId=\"a\" " +
                "testName=\"a\" outcome=\"Passed\"><Output><StdOut>");
            WriteRepeated(writer, characterCount, "diagnostic-output");
            writer.Write("</StdOut></Output></UnitTestResult></Results><TestDefinitions>");
            writer.Write(Definition("a", "a", "FEAT-01", "FEAT-01-001"));
            writer.Write("</TestDefinitions></TestRun>");
            return path;
        }

        public string WriteManyObservations(string name, int count)
        {
            var path = Path.Combine(Root, name);
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false), 64 * 1024);
            writer.Write("<TestRun id=\"many\"><TestDefinitions>");
            for (var index = 0; index < count; index++)
            {
                writer.Write(Definition($"test-{index}", $"Definition {index}",
                    "FEAT-01", "FEAT-01-001"));
            }

            writer.Write("</TestDefinitions><Results>");
            for (var index = 0; index < count; index++)
            {
                writer.Write(Result($"test-{index}", $"Test {index}", "Passed"));
            }

            writer.Write("</Results></TestRun>");
            return path;
        }

        public string WriteLargeName(string name, int characterCount)
        {
            var path = Path.Combine(Root, name);
            using var writer = new StreamWriter(path, append: false, new UTF8Encoding(false), 64 * 1024);
            writer.Write("<TestRun id=\"large-name\"><Results><UnitTestResult testId=\"a\" testName=\"");
            WriteRepeated(writer, characterCount, "long-display-name");
            writer.Write("\" outcome=\"Passed\"/></Results><TestDefinitions>");
            writer.Write(Definition("a", "a", "FEAT-01", "FEAT-01-001"));
            writer.Write("</TestDefinitions></TestRun>");
            return path;
        }

        private static void WriteRepeated(StreamWriter writer, int characterCount, string text)
        {
            var chunk = new string(text[0], 8192);
            while (characterCount > 0)
            {
                var count = Math.Min(characterCount, chunk.Length);
                writer.Write(chunk.AsSpan(0, count));
                characterCount -= count;
            }
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
