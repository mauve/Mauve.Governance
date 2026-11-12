using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Mauve.Governance.Testing.TrxLogger;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Xunit;

namespace Mauve.Governance.Testing.TrxLogger.Tests;

public sealed class GovernanceTrxLoggerTests
{
    private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Streams_complete_results_before_definitions_and_publishes_only_on_completion()
    {
        using var fixture = new Fixture();
        var result = Result();
        result.DisplayName = "Parameterized execution (42)";
        result.Outcome = TestOutcome.Failed;
        result.ComputerName = "runner-host";
        result.Duration = TimeSpan.FromSeconds(1.25);
        result.StartTime = DateTimeOffset.Parse("2026-01-01T10:00:00+02:00");
        result.EndTime = result.StartTime + result.Duration;
        result.ErrorMessage = "failure <&>\r\n";
        result.ErrorStackTrace = " at Example.Run()\r\n at Caller()";
        result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, "stdout <&>\r\n"));
        result.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, "stderr \n"));
        result.Messages.Add(new TestResultMessage(TestResultMessage.DebugTraceCategory, "trace"));
        result.Messages.Add(new TestResultMessage(TestResultMessage.AdditionalInfoCategory, "extra"));
        result.Messages.Add(new TestResultMessage("unknown-kind", "unknown"));
        fixture.Logger.Record(result);
        Assert.False(File.Exists(fixture.Logger.OutputPath));
        var pending = File.ReadAllText(fixture.Logger.PendingPath);
        Assert.Contains("stdout", pending);
        Assert.DoesNotContain("TestDefinitions", pending);

        fixture.Complete();
        Assert.False(File.Exists(fixture.Logger.PendingPath));
        var doc = XDocument.Load(fixture.Logger.OutputPath);
        var execution = Assert.Single(doc.Descendants(Ns + "UnitTestResult"));
        Assert.Equal(result.DisplayName, execution.Attribute("testName")!.Value);
        Assert.Equal(result.ComputerName, execution.Attribute("computerName")!.Value);
        Assert.Equal(result.Duration.ToString("c"), execution.Attribute("duration")!.Value);
        Assert.Equal(result.StartTime, DateTimeOffset.Parse(execution.Attribute("startTime")!.Value));
        Assert.Equal(result.EndTime, DateTimeOffset.Parse(execution.Attribute("endTime")!.Value));
        Assert.Equal("stdout <&>\r\n", execution.Descendants(Ns + "StdOut").Single().Value);
        Assert.Equal("stderr \n", execution.Descendants(Ns + "StdErr").Single().Value);
        Assert.Equal("trace", execution.Descendants(Ns + "DebugTrace").Single().Value);
        Assert.Equal(result.ErrorMessage, execution.Descendants(Ns + "ErrorInfo").Single().Element(Ns + "Message")!.Value);
        Assert.Equal(result.ErrorStackTrace, execution.Descendants(Ns + "StackTrace").Single().Value);
        Assert.Equal(result.Messages.Select(message => "[" + message.Category + "] " + message.Text),
            execution.Descendants(Ns + "TextMessages")
            .Elements(Ns + "Message").Select(message => message.Value));
        var raw = XDocument.Load(fixture.Companion);
        Assert.Equal(result.Messages.Select(message => message.Category),
            raw.Descendants("Messages").Elements("Message").Elements("Category").Select(element => element.Value));
        Assert.Equal("Failed", doc.Descendants(Ns + "ResultSummary").Single().Attribute("outcome")!.Value);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Preserves_all_traits_custom_properties_and_source_navigation_without_old_limits()
    {
        using var fixture = new Fixture();
        var result = Result();
        result.TestCase.CodeFilePath = "/source/case.cs";
        result.TestCase.LineNumber = 123;
        for (var index = 0; index < 70; index++)
        {
            result.TestCase.Traits.Add(new Trait("Other" + index, new string('x', 5000)));
        }
        var property = TestProperty.Register("GovernanceTest.Custom", "custom", typeof(string[]), typeof(TestCase));
        result.TestCase.SetPropertyValue(property, new[] { "one", "", "three" });
        fixture.Logger.Record(result);
        fixture.Complete();
        var definition = XDocument.Load(fixture.Logger.OutputPath).Descendants(Ns + "UnitTest").Single();
        Assert.Equal(71, definition.Descendants(Ns + "Property").Count());
        var raw = XDocument.Load(fixture.Companion);
        Assert.Contains(raw.Descendants("Value"), value => value.Value == "/source/case.cs");
        Assert.Contains(raw.Descendants("Value"), value => value.Value == "123");
        var custom = raw.Descendants("Property").Single(value => value.Attribute("id")!.Value == property.Id);
        Assert.Equal(new[] { "one", "", "three" }, custom.Element("Value")!.Elements("Item").Select(value => value.Value));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Allows_more_than_old_result_and_retained_character_limits()
    {
        using var fixture = new Fixture();
        var result = Result();
        result.DisplayName = new string('n', 5000);
        for (var index = 0; index < 10_001; index++)
        {
            fixture.Logger.Record(result);
        }
        fixture.Complete();
        var count = 0;
        using var reader = XmlReader.Create(fixture.Logger.OutputPath);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "UnitTestResult")
            {
                count++;
            }
        }
        Assert.Equal(10_001, count);
        Assert.True(new FileInfo(fixture.Logger.OutputPath).Length > 32L * 1024 * 1024);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Copies_test_and_run_attachments_without_collisions_and_bundle_can_move()
    {
        using var fixture = new Fixture();
        var first = fixture.Attachment("first", "same.bin", [0, 1, 255]);
        var second = fixture.Attachment("second", "same.bin", [2, 3, 254]);
        var result = Result();
        result.Attachments.Add(first);
        result.Attachments.Add(second);
        fixture.Logger.Record(result);
        fixture.Complete(new Collection<AttachmentSet> { first });
        var moved = Path.Combine(fixture.Root, "moved");
        Directory.CreateDirectory(moved);
        var originalBundle = Path.ChangeExtension(fixture.Logger.OutputPath, ".files");
        Directory.Move(originalBundle, Path.Combine(moved, Path.GetFileName(originalBundle)));
        var trx = Path.Combine(moved, Path.GetFileName(fixture.Logger.OutputPath));
        File.Move(fixture.Logger.OutputPath, trx);
        var doc = XDocument.Load(trx);
        foreach (var reference in doc.Descendants(Ns + "ResultFile"))
        {
            Assert.True(File.Exists(Path.Combine(moved, reference.Attribute("path")!.Value)));
        }
        var raw = XDocument.Load(Path.Combine(moved, Path.GetFileName(originalBundle), "runner-data.xml"));
        var copied = raw.Descendants("Attachment").ToArray();
        Assert.Equal(3, copied.Length);
        Assert.Equal(3, copied.Select(attachment => attachment.Element("Path")!.Value).Distinct().Count());
        Assert.All(copied, attachment => Assert.Equal("payload", attachment.Element("Description")!.Value));
        Assert.Equal(new byte[] { 0, 1, 255 }, File.ReadAllBytes(Path.Combine(moved, copied[0].Element("Path")!.Value)));
        Assert.Equal(new byte[] { 2, 3, 254 }, File.ReadAllBytes(Path.Combine(moved, copied[1].Element("Path")!.Value)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Resolves_local_paths_carried_by_relative_attachment_uris(bool rooted)
    {
        using var fixture = new Fixture();
        var attachment = fixture.Attachment("original", "payload.bin", [1, 2, 3]);
        var source = attachment.Attachments.Single().Uri.LocalPath;
        var path = rooted ? source : Path.GetRelativePath(fixture.Root, source);
        attachment.Attachments.Clear();
        attachment.Attachments.Add(new UriDataAttachment(new Uri(path, UriKind.Relative), "payload"));
        var result = Result();
        result.TestCase.Source = Path.Combine(fixture.Root, "tests.dll");
        result.Attachments.Add(attachment);
        fixture.Logger.Record(result);
        fixture.Complete();
        var raw = XDocument.Load(fixture.Companion);
        var copied = raw.Descendants("Attachment").Single();
        Assert.Equal(source, copied.Element("ResolvedSource")!.Value);
        Assert.Equal(new byte[] { 1, 2, 3 },
            File.ReadAllBytes(Path.Combine(fixture.Root, copied.Element("Path")!.Value)));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Writes_children_immediately_with_supplied_relationship_ids_and_allows_parent_later()
    {
        using var fixture = new Fixture();
        var parentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var executionProperty = TestProperty.Register("ExecutionId", "execution", typeof(Guid), typeof(TestResult));
        var parentProperty = TestProperty.Register("ParentExecId", "parent", typeof(Guid), typeof(TestResult));
        var child = Result();
        child.SetPropertyValue(executionProperty, childId);
        child.SetPropertyValue(parentProperty, parentId);
        fixture.Logger.Record(child);
        Assert.Contains(childId.ToString(), File.ReadAllText(fixture.Logger.PendingPath));
        var parent = Result();
        parent.SetPropertyValue(executionProperty, parentId);
        fixture.Logger.Record(parent);
        fixture.Complete();
        var results = XDocument.Load(fixture.Logger.OutputPath).Descendants(Ns + "UnitTestResult").ToArray();
        Assert.Equal(childId.ToString(), results[0].Attribute("executionId")!.Value);
        Assert.Equal(parentId.ToString(), results[0].Attribute("parentExecutionId")!.Value);
        Assert.Equal(parentId.ToString(), results[1].Attribute("executionId")!.Value);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Rejects_conflicting_definition_but_allows_trait_reordering()
    {
        using var fixture = new Fixture();
        var first = Result();
        first.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-003"));
        fixture.Logger.Record(first);
        var reordered = Result(includeFeature: false);
        reordered.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-003"));
        reordered.TestCase.Traits.Add(new Trait("Feature", "FEAT-03"));
        fixture.Logger.Record(reordered);
        reordered.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-004"));
        Assert.StartsWith("MG-GOV-TRX-0003:", Assert.Throws<InvalidDataException>(
            () => fixture.Logger.Record(reordered)).Message);
        Assert.False(File.Exists(fixture.Logger.OutputPath));
        Assert.False(File.Exists(fixture.Logger.PendingPath));
        Assert.False(Directory.Exists(Path.ChangeExtension(fixture.Logger.OutputPath, ".files")));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Preserves_xml_invalid_text_as_exact_code_units_in_companion()
    {
        using var fixture = new Fixture();
        var result = Result();
        var invalid = "before\0\uD800after";
        result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, invalid));
        fixture.Logger.Record(result);
        fixture.Complete();
        Assert.Contains("preserved losslessly", File.ReadAllText(fixture.Logger.OutputPath));
        var text = XDocument.Load(fixture.Companion).Descendants("Messages")
            .Elements("Message").Elements("Text").Single();
        Assert.Equal("utf16-code-units-base64", text.Attribute("encoding")!.Value);
        var bytes = Convert.FromBase64String(text.Value);
        var chars = Enumerable.Range(0, bytes.Length / 2)
            .Select(index => (char)(bytes[index * 2] | bytes[index * 2 + 1] << 8)).ToArray();
        Assert.Equal(invalid, new string(chars));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Missing_attachment_prevents_publication_and_preserves_unrelated_files()
    {
        using var fixture = new Fixture();
        var unrelated = Path.Combine(fixture.Root, "keep.txt");
        File.WriteAllText(unrelated, "keep");
        var result = Result();
        var set = new AttachmentSet(new Uri("datacollector://governance"), "missing");
        set.Attachments.Add(new UriDataAttachment(new Uri(Path.Combine(fixture.Root, "missing.bin")), "missing"));
        result.Attachments.Add(set);
        Assert.StartsWith("MG-GOV-TRX-0006:", Assert.Throws<InvalidDataException>(
            () => fixture.Logger.Record(result)).Message);
        Assert.Throws<InvalidDataException>(() => fixture.Complete());
        Assert.False(File.Exists(fixture.Logger.OutputPath));
        Assert.False(File.Exists(fixture.Logger.PendingPath));
        Assert.Equal("keep", File.ReadAllText(unrelated));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Concurrent_result_callbacks_produce_complete_unique_executions()
    {
        using var fixture = new Fixture();
        Parallel.For(0, 100, _ => fixture.Logger.Record(Result()));
        fixture.Complete();
        var results = XDocument.Load(fixture.Logger.OutputPath).Descendants(Ns + "UnitTestResult").ToArray();
        Assert.Equal(100, results.Length);
        Assert.Equal(100, results.Select(result => result.Attribute("executionId")!.Value).Distinct().Count());
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Finalization_failure_preserves_existing_destination_and_removes_pending_data()
    {
        using var fixture = new Fixture();
        fixture.Logger.Record(Result());
        File.WriteAllText(fixture.Logger.OutputPath, "existing");
        Assert.StartsWith("MG-GOV-TRX-0006:", Assert.Throws<InvalidDataException>(
            () => fixture.Complete()).Message);
        Assert.Equal("existing", File.ReadAllText(fixture.Logger.OutputPath));
        Assert.False(File.Exists(fixture.Logger.PendingPath));
        Assert.False(File.Exists(fixture.Companion));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Unsupported_runner_property_fails_explicitly_instead_of_dropping_data()
    {
        using var fixture = new Fixture();
        var result = Result();
        var property = TestProperty.Register("GovernanceTest.Unsupported", "unsupported",
            typeof(object), typeof(TestResult));
        result.SetPropertyValue(property, new object());
        Assert.StartsWith("MG-GOV-TRX-0008:", Assert.Throws<InvalidDataException>(
            () => fixture.Logger.Record(result)).Message);
        Assert.False(File.Exists(fixture.Logger.OutputPath));
        Assert.False(File.Exists(fixture.Logger.PendingPath));
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Does_not_retain_runner_results_or_their_output_between_callbacks()
    {
        using var fixture = new Fixture();
        var (result, output) = WriteCollectibleResult(fixture.Logger);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(result.IsAlive);
        Assert.False(output.IsAlive);
        fixture.Complete();
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Event_callbacks_publish_one_report_and_preserve_run_errors()
    {
        var root = Path.Combine(Path.GetTempPath(), $"governance-events-{Guid.NewGuid():N}");
        using var logger = new GovernanceTrxLogger();
        try
        {
            var events = new Events();
            logger.Initialize(events, root);
            events.Start(new TestRunCriteria(new[] { "tests.dll" }, 1)
            {
                DebugEnabledForTestSession = true
            });
            events.Result(Result());
            events.Message("runner error");
            var error = new InvalidOperationException("completion error") { HelpLink = "https://example.test/help" };
            error.Data["diagnostic"] = "completion context";
            events.Complete(error);
            var doc = XDocument.Load(Assert.Single(Directory.GetFiles(root, "*.trx")));
            Assert.Equal("Failed", doc.Descendants(Ns + "ResultSummary").Single().Attribute("outcome")!.Value);
            var raw = XDocument.Load(Path.Combine(Path.ChangeExtension(logger.OutputPath, ".files"), "runner-data.xml"));
            Assert.Equal("runner error", raw.Descendants("RunMessage").Single().Element("Text")!.Value);
            Assert.Equal("tests.dll", raw.Descendants("RunStart").Single().Element("Sources")!.Element("Item")!.Value);
            Assert.Equal("True", raw.Descendants("DebugEnabledForTestSession").Single().Value);
            Assert.Equal(error.HelpLink, raw.Descendants("Exception").Single().Element("HelpLink")!.Value);
            Assert.Contains(raw.Descendants("Data").Single().Descendants("Value"),
                value => value.Value == "completion context");
        }
        finally
        {
            logger.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Result, WeakReference Output) WriteCollectibleResult(GovernanceTrxLogger logger)
    {
        var result = Result();
        var output = new string('o', 1_000_000);
        result.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
        logger.Record(result);
        return (new WeakReference(result), new WeakReference(output));
    }

    [Theory]
    [InlineData(true, false, "Aborted")]
    [InlineData(false, true, "NotExecuted")]
    [InlineData(false, false, "Completed")]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Empty_and_interrupted_runs_publish_truthful_status(bool aborted, bool canceled, string outcome)
    {
        using var fixture = new Fixture();
        fixture.Logger.Message(new TestRunMessageEventArgs(TestMessageLevel.Warning, "run warning"));
        fixture.Logger.Complete(new TestRunCompleteEventArgs(null, canceled, aborted, null, null, TimeSpan.Zero));
        var doc = XDocument.Load(fixture.Logger.OutputPath);
        Assert.Equal(outcome, doc.Descendants(Ns + "ResultSummary").Single().Attribute("outcome")!.Value);
        Assert.Equal("run warning", XDocument.Load(fixture.Companion).Descendants("RunMessage").Single().Element("Text")!.Value);
    }

    private static TestResult Result(bool includeFeature = true)
    {
        var test = new TestCase("Governance.Case", new Uri("executor://governance/tests"), "tests.dll")
        {
            DisplayName = "Governance.Case"
        };
        if (includeFeature)
        {
            test.Traits.Add(new Trait("Feature", "FEAT-03"));
        }
        return new TestResult(test) { Outcome = TestOutcome.Passed };
    }

    private sealed class Events : TestLoggerEvents
    {
        public override event EventHandler<TestRunMessageEventArgs>? TestRunMessage;
        public override event EventHandler<TestResultEventArgs>? TestResult;
        public override event EventHandler<TestRunCompleteEventArgs>? TestRunComplete;
        public override event EventHandler<TestRunStartEventArgs>? TestRunStart;
        public override event EventHandler<DiscoveryStartEventArgs>? DiscoveryStart { add { } remove { } }
        public override event EventHandler<TestRunMessageEventArgs>? DiscoveryMessage { add { } remove { } }
        public override event EventHandler<DiscoveredTestsEventArgs>? DiscoveredTests { add { } remove { } }
        public override event EventHandler<DiscoveryCompleteEventArgs>? DiscoveryComplete { add { } remove { } }

        internal void Result(TestResult result) => TestResult?.Invoke(this, new TestResultEventArgs(result));
        internal void Start(TestRunCriteria criteria) => TestRunStart?.Invoke(this, new TestRunStartEventArgs(criteria));
        internal void Message(string text) => TestRunMessage?.Invoke(this,
            new TestRunMessageEventArgs(TestMessageLevel.Error, text));
        internal void Complete(Exception? error = null) => TestRunComplete?.Invoke(this,
            new TestRunCompleteEventArgs(null, false, false, error, null, TimeSpan.Zero));
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), $"governance-logger-{Guid.NewGuid():N}");
        internal GovernanceTrxLogger Logger { get; } = new();
        internal string Companion => Path.Combine(Path.ChangeExtension(Logger.OutputPath, ".files"), "runner-data.xml");

        internal Fixture() => Logger.Start(Root);

        internal void Complete(Collection<AttachmentSet>? attachments = null) =>
            Logger.Complete(new TestRunCompleteEventArgs(null, false, false, null, attachments, TimeSpan.Zero));

        internal AttachmentSet Attachment(string folder, string name, byte[] bytes)
        {
            var directory = Path.Combine(Root, folder);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            File.WriteAllBytes(path, bytes);
            var set = new AttachmentSet(new Uri("datacollector://governance/" + folder), folder);
            set.Attachments.Add(new UriDataAttachment(new Uri(path), "payload"));
            return set;
        }

        public void Dispose()
        {
            Logger.Dispose();
            Directory.Delete(Root, recursive: true);
        }
    }
}
