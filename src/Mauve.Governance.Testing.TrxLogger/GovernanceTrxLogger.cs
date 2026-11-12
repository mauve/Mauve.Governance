using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

[assembly: InternalsVisibleTo("Mauve.Governance.Testing.TrxLogger.Tests")]

namespace Mauve.Governance.Testing.TrxLogger;

[FriendlyName("governance-trx")]
[ExtensionUri("logger://Mauve/GovernanceTrx/v1")]
public sealed class GovernanceTrxLogger : ITestLogger, IDisposable
{
    private const string Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private const string TestType = "13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b";
    private readonly object gate = new();
    private readonly Dictionary<Guid, Definition> definitions = [];
    private readonly Dictionary<Guid, (Guid Test, Guid Parent)> executions = [];
    private readonly Dictionary<TestOutcome, long> counters = [];
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid listId = Guid.NewGuid();
    private readonly DateTimeOffset created = DateTimeOffset.UtcNow;
    private XmlWriter? writer;
    private RunnerDataWriter? data;
    private InvalidDataException? failure;
    private bool finished;
    private bool runError;
    private string bundle = "";
    internal string OutputPath { get; private set; } = "";
    internal string PendingPath => OutputPath + ".pending";

    public void Initialize(TestLoggerEvents events, string testRunDirectory)
    {
        Start(testRunDirectory);
        events.TestRunStart += (_, args) => Execute(() =>
            data!.RunStart(args.TestRunCriteria));
        events.TestResult += (_, args) => Record(args.Result);
        events.TestRunMessage += (_, args) => Message(args);
        events.TestRunComplete += (_, args) => Complete(args);
    }

    internal void Start(string directory) => Execute(() =>
    {
        if (writer is not null || OutputPath.Length != 0)
        {
            throw new InvalidDataException("MG-GOV-TRX-0005: Logger is already initialized.");
        }
        Directory.CreateDirectory(directory);
        OutputPath = Path.GetFullPath(Path.Combine(directory, $"governance-{runId:N}.trx"));
        bundle = Path.ChangeExtension(OutputPath, ".files");
        Directory.CreateDirectory(bundle);
        data = new RunnerDataWriter(bundle);
        writer = XmlWriter.Create(new FileStream(PendingPath, FileMode.CreateNew,
            FileAccess.Write, FileShare.Read), new XmlWriterSettings
            {
                CloseOutput = true,
                Indent = true,
                NewLineHandling = NewLineHandling.Entitize
            });
        writer.WriteStartDocument();
        writer.WriteStartElement("TestRun", Namespace);
        Attribute("id", runId.ToString());
        Attribute("name", "Mauve Governance test run");
        writer.WriteStartElement("Times");
        Attribute("creation", created.ToString("o", CultureInfo.InvariantCulture));
        Attribute("queuing", created.ToString("o", CultureInfo.InvariantCulture));
        Attribute("start", created.ToString("o", CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteStartElement("Results");
        writer.Flush();
    });

    internal void Record(TestResult result) => Execute(() =>
    {
        RequireStarted();
        ArgumentNullException.ThrowIfNull(result);
        if (result.Messages.Any(message => message is null))
        {
            throw new InvalidDataException("MG-GOV-TRX-0002: Runner message is null.");
        }
        var test = result.TestCase;
        var definition = new Definition(
            Required(test.DisplayName, "test name"),
            Required(test.Source, "test source"),
            Required(test.FullyQualifiedName, "test identity"),
            Required(test.ExecutorUri?.ToString(), "executor URI"),
            test.Traits.Select(trait => new TraitValue(trait.Name, trait.Value))
                .OrderBy(trait => trait.Name, StringComparer.Ordinal)
                .ThenBy(trait => trait.Value, StringComparer.Ordinal).ToArray(),
            RunnerDataWriter.Snapshot(test));
        if (definitions.TryGetValue(test.Id, out var previous))
        {
            if (previous.Name != definition.Name || previous.Source != definition.Source ||
                previous.FullName != definition.FullName || previous.Executor != definition.Executor ||
                !previous.Traits.SequenceEqual(definition.Traits) ||
                previous.Properties != definition.Properties)
            {
                throw new InvalidDataException("MG-GOV-TRX-0003: Conflicting runner metadata for the same test ID.");
            }
        }
        else
        {
            definitions.Add(test.Id, definition);
        }

        var execution = PropertyGuid(result, "ExecutionId");
        if (execution == Guid.Empty)
        {
            execution = Guid.NewGuid();
        }
        var parent = PropertyGuid(result, "ParentExecId");
        if (!executions.TryAdd(execution, (test.Id, parent)))
        {
            throw new InvalidDataException("MG-GOV-TRX-0003: Duplicate runner execution ID.");
        }
        var files = data!.Result(result, execution);
        var name = string.IsNullOrEmpty(result.DisplayName) ? definition.Name : result.DisplayName;
        writer!.WriteStartElement("UnitTestResult");
        Attribute("testId", test.Id.ToString());
        Attribute("executionId", execution.ToString());
        if (parent != Guid.Empty)
        {
            Attribute("parentExecutionId", parent.ToString());
        }
        Attribute("testName", name);
        Attribute("testType", TestType);
        Attribute("testListId", listId.ToString());
        Attribute("computerName", result.ComputerName ?? Environment.MachineName);
        Attribute("outcome", result.Outcome.ToString());
        Attribute("duration", result.Duration.ToString("c", CultureInfo.InvariantCulture));
        Attribute("startTime", result.StartTime.ToString("o", CultureInfo.InvariantCulture));
        Attribute("endTime", result.EndTime.ToString("o", CultureInfo.InvariantCulture));
        var resultType = Property(result, "ResultType");
        if (resultType is not null)
        {
            Attribute("resultType", Convert.ToString(resultType, CultureInfo.InvariantCulture));
        }
        var row = Property(result, "DataRowInfo");
        if (row is not null)
        {
            Attribute("dataRowInfo", Convert.ToString(row, CultureInfo.InvariantCulture));
        }
        writer.WriteStartElement("Output");
        WriteMessages(result, TestResultMessage.StandardOutCategory, "StdOut");
        WriteMessages(result, TestResultMessage.StandardErrorCategory, "StdErr");
        WriteMessages(result, TestResultMessage.DebugTraceCategory, "DebugTrace");
        if (result.ErrorMessage is not null || result.ErrorStackTrace is not null)
        {
            writer.WriteStartElement("ErrorInfo");
            Element("Message", result.ErrorMessage);
            Element("StackTrace", result.ErrorStackTrace);
            writer.WriteEndElement();
        }
        writer.WriteStartElement("TextMessages");
        foreach (var message in result.Messages)
        {
            writer.WriteStartElement("Message");
            writer.WriteString(XmlText("[" + message.Category + "] "));
            writer.WriteString(XmlText(message.Text ?? ""));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        WriteFiles(files);
        writer.WriteEndElement();
        counters[result.Outcome] = counters.GetValueOrDefault(result.Outcome) + 1;
        writer.Flush();
        data.Flush();
    });

    internal void Message(TestRunMessageEventArgs args) => Execute(() =>
    {
        RequireStarted();
        data!.Message(args);
        runError |= args.Level == TestMessageLevel.Error;
    });

    internal void Complete(TestRunCompleteEventArgs args) => Execute(() =>
    {
        RequireStarted();
        var runFiles = data!.Complete(args);
        data.Dispose();
        data = null;
        writer!.WriteEndElement();
        writer.WriteStartElement("TestDefinitions");
        foreach (var (id, definition) in definitions)
        {
            writer.WriteStartElement("UnitTest");
            Attribute("id", id.ToString());
            Attribute("name", definition.Name);
            Attribute("storage", definition.Source);
            writer.WriteStartElement("Properties");
            foreach (var trait in definition.Traits)
            {
                writer.WriteStartElement("Property");
                Element("Key", trait.Name);
                Element("Value", trait.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteStartElement("TestMethod");
            Attribute("codeBase", definition.Source);
            Attribute("adapterTypeName", definition.Executor);
            var dot = definition.FullName.LastIndexOf('.');
            Attribute("className", dot < 0 ? "" : definition.FullName[..dot]);
            Attribute("name", dot < 0 ? definition.FullName : definition.FullName[(dot + 1)..]);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("TestEntries");
        foreach (var (execution, entry) in executions)
        {
            writer.WriteStartElement("TestEntry");
            Attribute("testId", entry.Test.ToString());
            Attribute("executionId", execution.ToString());
            Attribute("testListId", listId.ToString());
            if (entry.Parent != Guid.Empty)
            {
                Attribute("parentExecutionId", entry.Parent.ToString());
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteStartElement("TestLists");
        writer.WriteStartElement("TestList");
        Attribute("name", "Results Not in a List");
        Attribute("id", listId.ToString());
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("ResultSummary");
        Attribute("outcome", args.IsAborted ? "Aborted" : args.IsCanceled ? "NotExecuted" :
            args.Error is not null || runError || counters.GetValueOrDefault(TestOutcome.Failed) > 0
                ? "Failed" : "Completed");
        writer.WriteStartElement("Counters");
        Attribute("total", executions.Count.ToString(CultureInfo.InvariantCulture));
        Attribute("executed", (counters.GetValueOrDefault(TestOutcome.Passed) +
            counters.GetValueOrDefault(TestOutcome.Failed)).ToString(CultureInfo.InvariantCulture));
        Attribute("passed", counters.GetValueOrDefault(TestOutcome.Passed).ToString(CultureInfo.InvariantCulture));
        Attribute("failed", counters.GetValueOrDefault(TestOutcome.Failed).ToString(CultureInfo.InvariantCulture));
        Attribute("notExecuted", (counters.GetValueOrDefault(TestOutcome.None) +
            counters.GetValueOrDefault(TestOutcome.Skipped)).ToString(CultureInfo.InvariantCulture));
        Attribute("notRunnable", counters.GetValueOrDefault(TestOutcome.NotFound).ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        if (args.Error is not null)
        {
            writer.WriteStartElement("Output");
            writer.WriteStartElement("ErrorInfo");
            Element("Message", args.Error.Message);
            Element("StackTrace", args.Error.ToString());
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        WriteFiles(runFiles);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Dispose();
        writer = null;
        File.Move(PendingPath, OutputPath);
        definitions.Clear();
        executions.Clear();
        finished = true;
    });

    private void WriteMessages(TestResult result, string category, string element)
    {
        if (!result.Messages.Any(message => string.Equals(message.Category, category,
            StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        writer!.WriteStartElement(element);
        foreach (var message in result.Messages.Where(message => string.Equals(message.Category,
            category, StringComparison.OrdinalIgnoreCase)))
        {
            writer.WriteString(XmlText(message.Text ?? ""));
        }
        writer.WriteEndElement();
    }

    private void WriteFiles(IEnumerable<string> paths)
    {
        writer!.WriteStartElement("ResultFiles");
        foreach (var path in paths)
        {
            writer.WriteStartElement("ResultFile");
            Attribute("path", path);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private void Element(string name, string? value)
    {
        if (value is not null)
        {
            writer!.WriteElementString(name, XmlText(value));
        }
    }

    private void Attribute(string name, string? value) =>
        writer!.WriteAttributeString(name, XmlText(value ?? ""));

    private static string XmlText(string value)
    {
        try
        {
            XmlConvert.VerifyXmlChars(value);
            return value;
        }
        catch (XmlException)
        {
            return "[XML-invalid text preserved losslessly in runner-data.xml]";
        }
    }

    private static string Required(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value) ? value :
            throw new InvalidDataException($"MG-GOV-TRX-0002: Missing {field}.");

    private static object? Property(TestObject value, string id) =>
        value.Properties.FirstOrDefault(property => property.Id == id) is { } property
            ? value.GetPropertyValue(property) : null;

    private static Guid PropertyGuid(TestObject value, string id) =>
        Property(value, id) switch
        {
            null => Guid.Empty,
            Guid guid => guid,
            string text when Guid.TryParse(text, out var guid) => guid,
            _ => throw new InvalidDataException($"MG-GOV-TRX-0002: Invalid runner {id}.")
        };

    private void RequireStarted()
    {
        if (writer is null || data is null)
        {
            throw new InvalidDataException("MG-GOV-TRX-0005: Logger has no active run.");
        }
    }

    private void Execute(Action action)
    {
        lock (gate)
        {
            if (failure is not null)
            {
                throw failure;
            }
            if (finished)
            {
                throw new InvalidDataException("MG-GOV-TRX-0005: Logger run has already ended.");
            }
            try
            {
                action();
            }
            catch (Exception error) when (error is IOException or InvalidDataException or
                XmlException or ArgumentException or InvalidOperationException or
                FormatException or NotSupportedException or UnauthorizedAccessException)
            {
                failure = error is InvalidDataException invalid &&
                    invalid.Message.StartsWith("MG-GOV-TRX-", StringComparison.Ordinal)
                    ? invalid : new InvalidDataException("MG-GOV-TRX-0006: Could not write complete runner TRX.", error);
                Console.Error.WriteLine(failure.Message);
                try
                {
                    Cleanup();
                }
                catch (Exception cleanupError) when (cleanupError is IOException or XmlException or
                    UnauthorizedAccessException)
                {
                    failure = new InvalidDataException("MG-GOV-TRX-0006: Runner TRX failed and cleanup also failed.",
                        new AggregateException(failure, cleanupError));
                }
                throw failure;
            }
        }
    }

    private void Cleanup()
    {
        try
        {
            writer?.Dispose();
        }
        finally
        {
            writer = null;
            try
            {
                data?.Dispose();
            }
            finally
            {
                data = null;
                definitions.Clear();
                executions.Clear();
                if (!finished && OutputPath.Length != 0)
                {
                    if (File.Exists(PendingPath))
                    {
                        File.Delete(PendingPath);
                    }
                    if (Directory.Exists(bundle))
                    {
                        Directory.Delete(bundle, recursive: true);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            Cleanup();
            finished = true;
        }
    }

    private sealed record TraitValue(string Name, string Value);
    private sealed record Definition(string Name, string Source, string FullName,
        string Executor, TraitValue[] Traits, string Properties);
}
