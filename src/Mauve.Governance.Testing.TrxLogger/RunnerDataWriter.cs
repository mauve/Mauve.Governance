using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Mauve.Governance.Testing.TrxLogger;

// A permanent attachment preserves runner data that TRX cannot express losslessly.
internal sealed class RunnerDataWriter : IDisposable
{
    private readonly string directory;
    private readonly XmlWriter writer;
    private bool closed;

    internal RunnerDataWriter(string directory)
    {
        this.directory = directory;
        writer = XmlWriter.Create(new FileStream(Path.Combine(directory, "runner-data.xml"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read), new XmlWriterSettings
            {
                CloseOutput = true,
                NewLineHandling = NewLineHandling.Entitize
            });
        writer.WriteStartDocument();
        writer.WriteStartElement("RunnerData");
        writer.WriteAttributeString("schemaVersion", "1");
    }

    internal static string Snapshot(TestCase test)
    {
        var text = new StringBuilder();
        using (var snapshot = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            snapshot.WriteStartElement("TestCase");
            Properties(snapshot, test, canonicalTraits: true);
            snapshot.WriteEndElement();
        }
        return text.ToString();
    }

    internal string[] Result(TestResult result, Guid execution)
    {
        writer.WriteStartElement("Result");
        writer.WriteAttributeString("executionId", execution.ToString());
        writer.WriteAttributeString("testId", result.TestCase.Id.ToString());
        writer.WriteStartElement("TestCase");
        Properties(writer, result.TestCase);
        writer.WriteEndElement();
        Properties(writer, result);
        writer.WriteStartElement("Messages");
        foreach (var message in result.Messages)
        {
            writer.WriteStartElement("Message");
            Field(writer, "Category", message.Category);
            Field(writer, "Text", message.Text);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        var files = Attachments(result.Attachments, execution.ToString("N"),
            Path.GetDirectoryName(Path.GetFullPath(result.TestCase.Source))!);
        writer.WriteEndElement();
        return [.. files, Path.GetFileName(directory) + "/runner-data.xml"];
    }

    internal void Message(TestRunMessageEventArgs args)
    {
        writer.WriteStartElement("RunMessage");
        Field(writer, "Level", args.Level);
        Field(writer, "Text", args.Message);
        writer.WriteEndElement();
        writer.Flush();
    }

    internal void RunStart(TestRunCriteria criteria)
    {
        writer.WriteStartElement("RunStart");
        Field(writer, "Settings", criteria.TestRunSettings);
        Field(writer, "KeepAlive", criteria.KeepAlive);
        Field(writer, "FrequencyOfRunStatsChangeEvent", criteria.FrequencyOfRunStatsChangeEvent);
        Field(writer, "RunStatsChangeEventTimeout", criteria.RunStatsChangeEventTimeout);
        Field(writer, "TestCaseFilter", criteria.TestCaseFilter);
        Field(writer, "Sources", criteria.Sources);
        Field(writer, "AdapterSourceMap", criteria.AdapterSourceMap);
        Field(writer, "HasSpecificSources", criteria.HasSpecificSources);
        Field(writer, "HasSpecificTests", criteria.HasSpecificTests);
        Field(writer, "SessionId", criteria.TestSessionInfo?.Id);
        Field(writer, "DebugEnabledForTestSession", criteria.DebugEnabledForTestSession);
        Field(writer, "TestHostLauncherType", criteria.TestHostLauncher?.GetType().AssemblyQualifiedName);
        Field(writer, "TestHostLauncherIsDebug", criteria.TestHostLauncher?.IsDebug);
        writer.WriteStartElement("FilterOptions");
        if (criteria.FilterOptions is { } filter)
        {
            Field(writer, "FilterRegEx", filter.FilterRegEx);
            Field(writer, "FilterRegExReplacement", filter.FilterRegExReplacement);
        }
        else
        {
            writer.WriteAttributeString("null", "true");
        }
        writer.WriteEndElement();
        if (criteria.Tests is not null)
        {
            writer.WriteStartElement("Tests");
            foreach (var test in criteria.Tests)
            {
                writer.WriteStartElement("TestCase");
                Properties(writer, test);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.Flush();
    }

    internal string[] Complete(TestRunCompleteEventArgs args)
    {
        writer.WriteStartElement("Completion");
        Field(writer, "Finished", DateTimeOffset.UtcNow);
        Field(writer, "IsCanceled", args.IsCanceled);
        Field(writer, "IsAborted", args.IsAborted);
        Field(writer, "ElapsedTime", args.ElapsedTimeInRunningTests);
        Field(writer, "Error", args.Error?.ToString());
        ExceptionData(args.Error);
        Field(writer, "Metrics", args.Metrics);
        Field(writer, "DiscoveredExtensions", args.DiscoveredExtensions);
        if (args.TestRunStatistics is { } statistics)
        {
            Field(writer, "ExecutedTests", statistics.ExecutedTests);
            Field(writer, "Statistics", statistics.Stats);
        }
        writer.WriteStartElement("InvokedDataCollectors");
        foreach (var collector in args.InvokedDataCollectors)
        {
            writer.WriteStartElement("Collector");
            Field(writer, "Uri", collector.Uri);
            Field(writer, "FriendlyName", collector.FriendlyName);
            Field(writer, "AssemblyQualifiedName", collector.AssemblyQualifiedName);
            Field(writer, "FilePath", collector.FilePath);
            Field(writer, "HasAttachmentProcessor", collector.HasAttachmentProcessor);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        var files = Attachments(args.AttachmentSets, "run", Path.GetDirectoryName(directory)!);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        return [.. files, Path.GetFileName(directory) + "/runner-data.xml"];
    }

    private void ExceptionData(Exception? error)
    {
        writer.WriteStartElement("Exception");
        if (error is null)
        {
            writer.WriteAttributeString("null", "true");
        }
        else
        {
            Field(writer, "Type", error.GetType().AssemblyQualifiedName);
            Field(writer, "Message", error.Message);
            Field(writer, "StackTrace", error.StackTrace);
            Field(writer, "Source", error.Source);
            Field(writer, "HelpLink", error.HelpLink);
            Field(writer, "HResult", error.HResult);
            Field(writer, "Data", error.Data);
            if (error is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    ExceptionData(inner);
                }
            }
            else if (error.InnerException is { } inner)
            {
                ExceptionData(inner);
            }
        }
        writer.WriteEndElement();
    }

    private List<string> Attachments(IEnumerable<AttachmentSet> sets, string scope, string baseDirectory)
    {
        var files = new List<string>();
        writer.WriteStartElement("Attachments");
        foreach (var set in sets)
        {
            ArgumentNullException.ThrowIfNull(set);
            writer.WriteStartElement("Set");
            Field(writer, "Uri", set.Uri);
            Field(writer, "DisplayName", set.DisplayName);
            foreach (var attachment in set.Attachments)
            {
                ArgumentNullException.ThrowIfNull(attachment);
                if (attachment.Uri.IsAbsoluteUri && !attachment.Uri.IsFile)
                {
                    throw new InvalidDataException("MG-GOV-TRX-0007: Attachment must identify a local file.");
                }
                var source = attachment.Uri.IsAbsoluteUri ? attachment.Uri.LocalPath :
                    Path.GetFullPath(attachment.Uri.OriginalString, baseDirectory);
                var name = Path.GetFileName(source);
                var relative = scope + "/" + Guid.NewGuid().ToString("N") + "/" + name;
                var destination = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using (var input = new FileStream(source, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write))
                {
                    input.CopyTo(output);
                }
                var path = Path.GetFileName(directory) + "/" + relative;
                files.Add(path);
                writer.WriteStartElement("Attachment");
                Field(writer, "OriginalUri", attachment.Uri);
                Field(writer, "ResolvedSource", source);
                Field(writer, "Description", attachment.Description);
                Field(writer, "Path", path);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        return files;
    }

    private static void Properties(XmlWriter target, TestObject value, bool canonicalTraits = false)
    {
        target.WriteStartElement("Properties");
        foreach (var property in value.Properties.OrderBy(property => property.Id, StringComparer.Ordinal))
        {
            target.WriteStartElement("Property");
            target.WriteAttributeString("id", property.Id);
            Field(target, "Label", property.Label);
            Field(target, "Category", property.Category);
            Field(target, "Description", property.Description);
            Field(target, "Attributes", property.Attributes);
            Field(target, "ValueType", property.GetValueType()?.AssemblyQualifiedName);
            var propertyValue = value.GetPropertyValue(property);
            if (canonicalTraits && property.Id == "TestObject.Traits" &&
                propertyValue is KeyValuePair<string, string>[] traits)
            {
                propertyValue = traits.OrderBy(trait => trait.Key, StringComparer.Ordinal)
                    .ThenBy(trait => trait.Value, StringComparer.Ordinal).ToArray();
            }
            Field(target, "Value", propertyValue);
            target.WriteEndElement();
        }
        target.WriteEndElement();
    }

    private static void Field(XmlWriter target, string name, object? value)
    {
        target.WriteStartElement(name);
        Value(target, value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        target.WriteEndElement();
    }

    private static void Value(XmlWriter target, object? value, HashSet<object> visiting)
    {
        if (value is null)
        {
            target.WriteAttributeString("null", "true");
            return;
        }
        target.WriteAttributeString("type", value.GetType().AssemblyQualifiedName);
        switch (value)
        {
            case string text:
                Text(target, text);
                return;
            case char character:
                Text(target, character.ToString());
                return;
            case Uri uri:
                Text(target, uri.OriginalString);
                return;
            case Guid guid:
                target.WriteString(guid.ToString());
                return;
            case DateTime date:
                target.WriteString(date.ToString("o", CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset date:
                target.WriteString(date.ToString("o", CultureInfo.InvariantCulture));
                return;
            case TimeSpan duration:
                target.WriteString(duration.ToString("c", CultureInfo.InvariantCulture));
                return;
            case Enum enumeration:
                target.WriteString(enumeration.ToString("D"));
                return;
            case KeyValuePair<string, string> pair:
                Field(target, "Key", pair.Key);
                Field(target, "Value", pair.Value);
                return;
            case bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                target.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture)!);
                return;
            case float number:
                target.WriteString(number.ToString("R", CultureInfo.InvariantCulture));
                return;
            case double number:
                target.WriteString(number.ToString("R", CultureInfo.InvariantCulture));
                return;
        }
        if (!visiting.Add(value))
        {
            throw new InvalidDataException("MG-GOV-TRX-0008: Runner property contains a reference cycle.");
        }
        try
        {
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    target.WriteStartElement("Entry");
                    target.WriteStartElement("Key");
                    Value(target, item.Key, visiting);
                    target.WriteEndElement();
                    target.WriteStartElement("Value");
                    Value(target, item.Value, visiting);
                    target.WriteEndElement();
                    target.WriteEndElement();
                }
            }
            else if (value is IEnumerable sequence)
            {
                foreach (var item in sequence)
                {
                    target.WriteStartElement("Item");
                    Value(target, item, visiting);
                    target.WriteEndElement();
                }
            }
            else
            {
                throw new InvalidDataException(
                    $"MG-GOV-TRX-0008: Unsupported runner property type {value.GetType().FullName}.");
            }
        }
        finally
        {
            visiting.Remove(value);
        }
    }

    private static void Text(XmlWriter target, string text)
    {
        try
        {
            XmlConvert.VerifyXmlChars(text);
            target.WriteString(text);
        }
        catch (XmlException)
        {
            target.WriteAttributeString("encoding", "utf16-code-units-base64");
            var buffer = new byte[8192];
            for (var offset = 0; offset < text.Length;)
            {
                var count = Math.Min(buffer.Length / 2, text.Length - offset);
                for (var index = 0; index < count; index++)
                {
                    var character = text[offset + index];
                    buffer[index * 2] = (byte)character;
                    buffer[index * 2 + 1] = (byte)(character >> 8);
                }
                target.WriteBase64(buffer, 0, count * 2);
                offset += count;
            }
        }
    }

    internal void Flush() => writer.Flush();

    public void Dispose()
    {
        if (!closed)
        {
            closed = true;
            writer.Dispose();
        }
    }
}
