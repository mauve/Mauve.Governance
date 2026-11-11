using System;
using System.Collections.Generic;
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
public sealed class GovernanceTrxLogger : ITestLogger
{
    internal const int MaximumResults = 10_000;
    internal const int MaximumTraits = 64;
    internal const int MaximumFieldLength = 4_096;
    internal const int MaximumRetainedCharacters = 2_000_000;
    private readonly List<ResultSnapshot> results = [];
    private readonly Dictionary<Guid, DefinitionSnapshot> definitions = [];
    private readonly object gate = new();
    private int retainedCharacters;
    private InvalidDataException? failure;
    private string directory = "";

    public void Initialize(TestLoggerEvents events, string testRunDirectory)
    {
        directory = testRunDirectory;
        events.TestResult += (_, args) =>
        {
            lock (gate)
            {
                if (failure is null)
                {
                    try
                    {
                        Record(args.Result);
                    }
                    catch (InvalidDataException error)
                    {
                        failure = error;
                        results.Clear();
                        definitions.Clear();
                    }
                }
            }
        };
        events.TestRunComplete += (_, _) =>
        {
            lock (gate)
            {
                if (failure is not null)
                {
                    throw failure;
                }
                Write();
            }
        };
    }

    internal void Record(TestResult result)
    {
        if (results.Count >= MaximumResults)
        {
            throw new InvalidDataException("MG-GOV-TRX-0001: Runner result count exceeds the limit.");
        }

        var test = result.TestCase;
        var name = Bounded(test.DisplayName, "test name");
        var source = Bounded(test.Source, "test source");
        var fullName = Bounded(test.FullyQualifiedName, "test identity");
        var executor = Bounded(test.ExecutorUri?.ToString(), "executor URI");
        var outcome = Bounded(result.Outcome.ToString(), "test outcome");
        var traits = new List<TraitSnapshot>();
        var traitCount = 0;
        foreach (var trait in test.Traits)
        {
            if (++traitCount > MaximumTraits)
            {
                throw new InvalidDataException("MG-GOV-TRX-0002: Test has too many runner traits.");
            }
            if (trait.Name is "Feature" or "Requirement")
            {
                traits.Add(new TraitSnapshot(trait.Name, Bounded(trait.Value, "test metadata")));
            }
        }

        var id = test.Id;
        var definition = new DefinitionSnapshot(name, source, fullName, executor,
            traits.OrderBy(trait => trait.Name, StringComparer.Ordinal)
                .ThenBy(trait => trait.Value, StringComparer.Ordinal).ToArray());
        var isNew = !definitions.TryGetValue(id, out var previous);
        if (!isNew && (previous!.Name != definition.Name ||
            previous.Source != definition.Source ||
            previous.FullName != definition.FullName ||
            previous.Executor != definition.Executor ||
            !previous.Traits.SequenceEqual(definition.Traits)))
        {
            throw new InvalidDataException("MG-GOV-TRX-0003: Conflicting runner metadata for the same test ID.");
        }

        var extra = name.Length + outcome.Length;
        if (isNew)
        {
            extra += source.Length + fullName.Length + executor.Length + name.Length +
                definition.Traits.Sum(trait => trait.Name.Length + trait.Value.Length);
        }
        if (extra > MaximumRetainedCharacters - retainedCharacters)
        {
            throw new InvalidDataException("MG-GOV-TRX-0004: Runner TRX metadata exceeds the size limit.");
        }

        retainedCharacters += extra;
        if (isNew)
        {
            definitions.Add(id, definition);
        }
        results.Add(new ResultSnapshot(id, name, outcome));
    }

    private static string Bounded(string? value, string field) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumFieldLength
            ? value
            : throw new InvalidDataException($"MG-GOV-TRX-0002: Invalid or oversized {field}.");

    private void Write()
    {
        if (results.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"governance-{Guid.NewGuid():N}.trx");
        var pending = path + ".pending";
        var listId = Guid.NewGuid().ToString();
        const string testType = "13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b";
        try
        {
            using (var writer = XmlWriter.Create(pending, new XmlWriterSettings { Indent = true }))
            {
                const string ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
                writer.WriteStartDocument();
                writer.WriteStartElement("TestRun", ns);
                writer.WriteAttributeString("id", Guid.NewGuid().ToString());
                writer.WriteAttributeString("name", "Mauve Governance test run");
                writer.WriteStartElement("Results");
                foreach (var result in results)
                {
                    writer.WriteStartElement("UnitTestResult");
                    writer.WriteAttributeString("testId", result.Id.ToString());
                    writer.WriteAttributeString("testName", result.Name);
                    writer.WriteAttributeString("testType", testType);
                    writer.WriteAttributeString("testListId", listId);
                    writer.WriteAttributeString("computerName", Environment.MachineName);
                    writer.WriteAttributeString("outcome", result.Outcome);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("TestDefinitions");
                foreach (var (id, definition) in definitions)
                {
                    writer.WriteStartElement("UnitTest");
                    writer.WriteAttributeString("id", id.ToString());
                    writer.WriteAttributeString("name", definition.Name);
                    writer.WriteStartElement("Properties");
                    foreach (var trait in definition.Traits)
                    {
                        writer.WriteStartElement("Property");
                        writer.WriteElementString("Key", trait.Name);
                        writer.WriteElementString("Value", trait.Value);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteStartElement("TestMethod");
                    writer.WriteAttributeString("codeBase", definition.Source);
                    writer.WriteAttributeString("className", definition.FullName);
                    writer.WriteAttributeString("name", definition.Name);
                    writer.WriteAttributeString("adapterTypeName", definition.Executor);
                    writer.WriteEndElement();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("TestLists");
                writer.WriteStartElement("TestList");
                writer.WriteAttributeString("name", "Results Not in a List");
                writer.WriteAttributeString("id", listId);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("ResultSummary");
                writer.WriteAttributeString("outcome", "Completed");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
            if (new FileInfo(pending).Length > 32 * 1024 * 1024)
            {
                throw new InvalidDataException("MG-GOV-TRX-0004: Emitted TRX exceeds the parser size limit.");
            }
            File.Move(pending, path);
        }
        finally
        {
            if (File.Exists(pending))
            {
                File.Delete(pending);
            }
        }
    }

    private sealed record ResultSnapshot(Guid Id, string Name, string Outcome);
    private sealed record TraitSnapshot(string Name, string Value);
    private sealed record DefinitionSnapshot(string Name, string Source, string FullName,
        string Executor, TraitSnapshot[] Traits);
}
