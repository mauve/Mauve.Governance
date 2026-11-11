using System.Collections.Immutable;
using Mauve.Governance.Logging.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Mauve.Governance.Logging.Analyzers.Tests;

public sealed class StructuredLoggingAnalyzerTests
{
    private const string Api = """
        namespace Microsoft.Extensions.Logging {
            public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical, None }
            public readonly struct EventId {
                public EventId(int id, string? name = null) { Id = id; }
                public int Id { get; }
                public static implicit operator EventId(int id) => new(id);
            }
            public interface ILogger {
                void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                    System.Exception? exception, System.Func<TState, System.Exception?, string> formatter);
            }
            public static class LoggerExtensions {
                public static void Log(this ILogger logger, LogLevel logLevel, string message, params object[] args) {}
                public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, string message, params object[] args) {}
                public static void Log(this ILogger logger, LogLevel logLevel, System.Exception exception, string message, params object[] args) {}
                public static void Log(this ILogger logger, LogLevel logLevel, EventId eventId, System.Exception exception, string message, params object[] args) {}
                public static void LogInformation(this ILogger logger, string message, params object[] args) {}
                public static void LogInformation(this ILogger logger, EventId eventId, string message, params object[] args) {}
                public static void LogInformation(this ILogger logger, System.Exception exception, string message, params object[] args) {}
                public static void LogInformation(this ILogger logger, EventId eventId, System.Exception exception, string message, params object[] args) {}
                public static void LogWarning(this ILogger logger, string message, params object[] args) {}
                public static void LogWarning(this ILogger logger, EventId eventId, string message, params object[] args) {}
                public static void LogWarning(this ILogger logger, System.Exception exception, string message, params object[] args) {}
                public static void LogWarning(this ILogger logger, EventId eventId, System.Exception exception, string message, params object[] args) {}
                public static void LogError(this ILogger logger, string message, params object[] args) {}
                public static void LogError(this ILogger logger, EventId eventId, string message, params object[] args) {}
                public static void LogError(this ILogger logger, System.Exception exception, string message, params object[] args) {}
                public static void LogError(this ILogger logger, EventId eventId, System.Exception exception, string message, params object[] args) {}
                public static void LogCritical(this ILogger logger, string message, params object[] args) {}
                public static void LogCritical(this ILogger logger, EventId eventId, string message, params object[] args) {}
                public static void LogCritical(this ILogger logger, System.Exception exception, string message, params object[] args) {}
                public static void LogCritical(this ILogger logger, EventId eventId, System.Exception exception, string message, params object[] args) {}
                public static void LogDebug(this ILogger logger, string message, params object[] args) {}
                public static void LogTrace(this ILogger logger, string message, params object[] args) {}
            }
            public static class LoggerMessage {
                public static System.Action<ILogger, T, System.Exception?> Define<T>(LogLevel logLevel,
                    EventId eventId, string formatString) => (_, _, _) => {};
                public static System.Action<ILogger, System.Exception?> Define(LogLevel logLevel,
                    EventId eventId, string formatString) => (_, _) => {};
            }
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class LoggerMessageAttribute : System.Attribute {
                public LoggerMessageAttribute() {}
                public LoggerMessageAttribute(int eventId, LogLevel level, string message) {}
                public int EventId { get; set; }
                public LogLevel Level { get; set; }
                public string Message { get; set; } = "";
            }
        }
        """;

    private const string Prefix = """
        using Microsoft.Extensions.Logging;
        using System;
        class T {
            const int Id = 41;
            void Log(ILogger logger, Exception ex, string text, int id, LogLevel level) {
        """;

    [Theory]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    [InlineData("logger.LogInformation(Id, \"Received {Count}\", 2);")]
    [InlineData("logger.Log(LogLevel.Information, Id, \"Received {Count}\", 2);")]
    [InlineData("logger.Log(LogLevel.Warning, new EventId(Id), ex, \"Warning {Count}\", 2);")]
    [InlineData("logger.Log(LogLevel.Error, Id, \"Error\");")]
    [InlineData("logger.Log(LogLevel.Critical, Id, ex, \"Failure {Code}\", 2);")]
    [InlineData("LoggerExtensions.Log(logger, LogLevel.Error, Id, ex, \"Failure {Code}\", 2);")]
    [InlineData("logger.Log(message: \"Failure {Code}\", exception: ex, eventId: Id, logLevel: LogLevel.Critical, args: new object[] { 2 });")]
    [InlineData("logger.Log(LogLevel.Debug, default(EventId), text);")]
    [InlineData("logger.Log(LogLevel.Trace, ex, text);")]
    [InlineData("logger.Log(LogLevel.None, 0, text);")]
    [InlineData("logger.LogWarning(new EventId(Id, \"Received\"), ex, \"Received {Count}\", 2);")]
    [InlineData("logger.LogError(Id, ex, \"Failure {Code}\", 2);")]
    [InlineData("logger.LogCritical(Id, \"Failure\");")]
    [InlineData("LoggerExtensions.LogWarning(logger, Id, \"Count {Count}\", 2);")]
    [InlineData("logger.LogCritical(message: \"Failed {Code}\", eventId: Id, args: new object[] { 2 });")]
    [InlineData("LoggerMessage.Define<int>(LogLevel.Error, new EventId(Id), \"Count {Count}\");")]
    [InlineData("LoggerMessage.Define(LogLevel.Information, Id, \"Started\");")]
    [InlineData("logger.LogDebug(\"Debug {Value}\", id); logger.LogTrace(\"Trace {Value}\", id);")]
    [InlineData("LoggerMessage.Define<int>(LogLevel.Debug, default, text);")]
    [InlineData("LoggerMessage.Define<int>(LogLevel.None, default, text);")]
    [InlineData("const string Template = \"Processing {Item}\"; logger.LogInformation(Id, Template, id);")]
    [InlineData("logger.LogInformation(Id, \"Value {Value:000} and {{literal}}\", id);")]
    public async Task AcceptsStableLoggingAndExemptLevels(string statement) =>
        Assert.Empty(await Analyze(Prefix + statement + " } }"));

    [Theory]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    [InlineData("logger.LogInformation(\"Started\");", 1, 0)]
    [InlineData("logger.Log(LogLevel.Information, \"Started\");", 1, 0)]
    [InlineData("logger.Log(LogLevel.Warning, ex, \"Warning\");", 1, 0)]
    [InlineData("logger.Log(LogLevel.Error, default(EventId), \"Error\");", 1, 0)]
    [InlineData("logger.Log(LogLevel.Critical, 0, ex, \"Failure\");", 1, 0)]
    [InlineData("logger.Log(LogLevel.Information, id, \"Received {Count}\", 2);", 1, 0)]
    [InlineData("logger.Log(LogLevel.Warning, Id, $\"Warning {id}\");", 0, 1)]
    [InlineData("logger.Log(LogLevel.Error, Id, ex, text);", 0, 1)]
    [InlineData("logger.Log(logLevel: level, args: new object[] { id }, message: text, exception: ex, eventId: id);", 1, 1)]
    [InlineData("LoggerExtensions.Log(logger, level, id, ex, text);", 1, 1)]
    [InlineData("logger.LogWarning(ex, \"Started\");", 1, 0)]
    [InlineData("logger.LogError(default(EventId), \"Failed\");", 1, 0)]
    [InlineData("logger.LogCritical(new EventId(0), \"Failed\");", 1, 0)]
    [InlineData("logger.LogInformation(id, \"Started\");", 1, 0)]
    [InlineData("logger.LogWarning(new EventId(id), ex, \"Failed\");", 1, 0)]
    [InlineData("logger.LogError(Id, $\"Failed {id}\");", 0, 1)]
    [InlineData("logger.LogCritical(Id, text);", 0, 1)]
    [InlineData("logger.LogInformation(Id, \"Failed \" + text);", 0, 1)]
    [InlineData("logger.LogWarning(args: new object[] { id }, message: text, eventId: Id);", 0, 1)]
    [InlineData("logger.LogInformation(Id, \"Value {0}\", id);", 0, 1)]
    [InlineData("logger.LogInformation(Id, \"Value {Missing\", id);", 0, 1)]
    [InlineData("logger.LogError(Id, \"\");", 0, 1)]
    [InlineData("LoggerMessage.Define<int>(LogLevel.Warning, 0, \"Value {Value}\");", 1, 0)]
    [InlineData("LoggerMessage.Define<int>(level, id, text);", 1, 1)]
    [InlineData("logger.Log<string>(LogLevel.Error, Id, \"Value {Value}\", null, (state, error) => state);", 0, 1)]
    [InlineData("logger.Log<string>(LogLevel.Information, default, text, null, (state, error) => state);", 1, 1)]
    public async Task ReportsUnstableIdsAndTemplates(string statement, int ids, int templates)
    {
        var result = await Analyze(Prefix + statement + " } }");
        Assert.Equal(ids, result.Count(d => d.Id == StructuredLoggingAnalyzer.InvalidEventId));
        Assert.Equal(templates, result.Count(d => d.Id == StructuredLoggingAnalyzer.InvalidTemplateId));
    }

    [Fact]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    public async Task IgnoresUnrelatedMethodsAndDebugDirectCalls()
    {
        Assert.Empty(await Analyze(Prefix + """
             new Other().LogInformation(text);
             new Other().Log(LogLevel.Critical, text);
             logger.Log<string>(LogLevel.Trace, default, text, null, (state, error) => state);
             LoggerMessage.Define<int>(LogLevel.Trace, default, text);
             } }
             class Other {
                 public void LogInformation(string message) {}
                 public void Log(LogLevel level, string message) {}
             }
             """));
    }

    [Fact]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    public async Task IgnoresSameNamedAttributesAndMethodsOutsideLogging()
    {
        Assert.Empty(await Analyze("""
            namespace Other {
                class LoggerMessageAttribute : System.Attribute {
                    public int EventId { get; set; }
                }
                class T {
                    [LoggerMessage(EventId = 0)]
                    void LogInformation(string message) {}
                }
            }
            """));
    }

    [Fact]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    public async Task ChecksConcreteILoggerImplementations()
    {
        var diagnostics = await Analyze("""
            using Microsoft.Extensions.Logging;
            using System;
            class Concrete : ILogger {
                public void Log<TState>(LogLevel level, EventId eventId, TState state,
                    Exception? exception, Func<TState, Exception?, string> formatter) {}
                void Write() => Log(LogLevel.Error, 0, "message", null, (state, error) => state);
            }
            """);
        Assert.Single(diagnostics.Where(d => d.Id == StructuredLoggingAnalyzer.InvalidEventId));
        Assert.Single(diagnostics.Where(d => d.Id == StructuredLoggingAnalyzer.InvalidTemplateId));
    }

    [Fact]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    public async Task ChecksSourceGeneratedDeclarationsIncludingPositionalArguments()
    {
        var result = await Analyze("""
            using Microsoft.Extensions.Logging;
            partial class Log {
                [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Order {OrderId}")]
                static partial void Good(ILogger logger, int orderId);
                [LoggerMessage(Level = LogLevel.Warning, Message = "Missing")]
                static partial void MissingId(ILogger logger);
                [LoggerMessage(0, LogLevel.Error, "Bad {0}")]
                static partial void Invalid(ILogger logger);
                [LoggerMessage(EventId = 0, Level = LogLevel.Debug)]
                static partial void Debug(ILogger logger);
                [LoggerMessage(Message = "Dynamic level", EventId = 0)]
                static partial void DynamicLevel(ILogger logger, LogLevel level);
            }
            """);
        Assert.Equal(3, result.Count(d => d.Id == StructuredLoggingAnalyzer.InvalidEventId));
        Assert.Single(result.Where(d => d.Id == StructuredLoggingAnalyzer.InvalidTemplateId));
    }

    [Fact]
    [Trait("Feature", "FEAT-04")]
    [Trait("Requirement", "FEAT-04-001")]
    public async Task DoesNotAnalyzeGeneratedTrees()
    {
        Assert.Empty(await Analyze(Prefix + "logger.LogError(0, text); } }",
            path: "Logging.g.cs"));
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(string source, string path = "Logging.cs")
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var compilation = CSharpCompilation.Create("Fixture",
            [CSharpSyntaxTree.ParseText(source + "\n" + Api, path: path)],
            platform.Select(file => MetadataReference.CreateFromFile(file)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers([new StructuredLoggingAnalyzer()]).GetAnalyzerDiagnosticsAsync();
    }
}
