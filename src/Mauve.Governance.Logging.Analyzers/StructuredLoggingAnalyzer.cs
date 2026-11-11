using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Mauve.Governance.Logging.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StructuredLoggingAnalyzer : DiagnosticAnalyzer
{
    public const string InvalidEventId = "MGLOG001";
    public const string InvalidTemplateId = "MGLOG002";

    private static readonly DiagnosticDescriptor EventIdRule = new(
        InvalidEventId, "Use a stable nonzero logging event ID",
        "Logging at Information or higher requires a nonzero compile-time numeric EventId",
        "Governance", DiagnosticSeverity.Warning, true);

    private static readonly DiagnosticDescriptor TemplateRule = new(
        InvalidTemplateId, "Use a stable structured logging template",
        "Logging at Information or higher requires a compile-time constant structured message template",
        "Governance", DiagnosticSeverity.Warning, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(EventIdRule, TemplateRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var logger = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.ILogger");
            var extensions = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerExtensions");
            var message = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerMessage");
            var attribute = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LoggerMessageAttribute");
            var eventId = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.EventId");
            var level = start.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Logging.LogLevel");
            if (logger == null || eventId == null || level == null)
            {
                return;
            }

            start.RegisterOperationAction(ctx =>
                AnalyzeInvocation(ctx, logger, extensions, message, eventId, level), OperationKind.Invocation);
            if (attribute != null)
            {
                start.RegisterSymbolAction(ctx => AnalyzeDeclaration(ctx, attribute, level), SymbolKind.Method);
            }
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol logger,
        INamedTypeSymbol? extensions, INamedTypeSymbol? message, INamedTypeSymbol eventId,
        INamedTypeSymbol level)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        IArgumentOperation? id = null;
        IArgumentOperation? template = null;
        var direct = false;

        if (extensions != null && SymbolEqualityComparer.Default.Equals(method.ContainingType, extensions) &&
            (IsNamedLevel(method.Name) || method.Name == "Log" && !method.IsGenericMethod &&
                method.Parameters.Length >= 4 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, level)) &&
            method.IsExtensionMethod &&
            method.Parameters.Length >= 2 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, logger))
        {
            if (method.Name == "Log" && !RequiresPolicy(Argument(invocation, method.Parameters[1])?.Value, level))
            {
                return;
            }

            id = Argument(invocation, "eventId", eventId);
            template = Argument(invocation, "message", SpecialType.System_String);
            if (!method.Parameters.Any(p => p.Name == "message" && p.Type.SpecialType == SpecialType.System_String))
            {
                return;
            }
        }
        else if (message != null && SymbolEqualityComparer.Default.Equals(method.ContainingType, message) &&
            method.Name == "Define" && method.IsStatic && method.Parameters.Length >= 3 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, level) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, eventId) &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_String)
        {
            if (!RequiresPolicy(Argument(invocation, method.Parameters[0])?.Value, level))
            {
                return;
            }

            id = Argument(invocation, method.Parameters[1]);
            template = Argument(invocation, method.Parameters[2]);
        }
        else if (method.Name == "Log" && method.IsGenericMethod &&
            method.Parameters.Length == 5 &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, level) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, eventId) &&
            IsLoggerLog(method, logger))
        {
            if (!RequiresPolicy(Argument(invocation, method.Parameters[0])?.Value, level))
            {
                return;
            }

            id = Argument(invocation, method.Parameters[1]);
            direct = true;
        }
        else
        {
            return;
        }

        if (!ValidEventId(id?.Value, eventId))
        {
            context.ReportDiagnostic(Diagnostic.Create(EventIdRule,
                id?.Syntax.GetLocation() ?? invocation.Syntax.GetLocation()));
        }

        // ILogger.Log<TState> accepts arbitrary state and formatters. Its message template
        // cannot be verified at the call site, even when the state happens to be a string.
        if (direct || !ValidTemplate(template?.Value))
        {
            context.ReportDiagnostic(Diagnostic.Create(TemplateRule,
                template?.Syntax.GetLocation() ?? invocation.Syntax.GetLocation()));
        }
    }

    private static bool IsLoggerLog(IMethodSymbol method, INamedTypeSymbol logger)
    {
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, logger))
        {
            return true;
        }

        if (!method.ContainingType.AllInterfaces.Contains(logger, SymbolEqualityComparer.Default))
        {
            return false;
        }

        return logger.GetMembers("Log").OfType<IMethodSymbol>().Any(member =>
            SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(member)?.OriginalDefinition,
                method.OriginalDefinition));
    }

    private static void AnalyzeDeclaration(SymbolAnalysisContext context, INamedTypeSymbol attribute,
        INamedTypeSymbol level)
    {
        var method = (IMethodSymbol)context.Symbol;
        foreach (var data in method.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(data.AttributeClass, attribute) ||
                data.ApplicationSyntaxReference == null)
            {
                continue;
            }

            var syntax = data.ApplicationSyntaxReference.GetSyntax(context.CancellationToken);
            var namedLevel = data.NamedArguments.FirstOrDefault(pair => pair.Key == "Level");
            var constructorLevel = data.AttributeConstructor?.Parameters
                .Select((p, i) => (p, i)).FirstOrDefault(pair => pair.p.Name == "level");
            var levelValue = namedLevel.Key == "Level" ? namedLevel.Value :
                constructorLevel.HasValue && constructorLevel.Value.p != null &&
                constructorLevel.Value.i < data.ConstructorArguments.Length
                    ? data.ConstructorArguments[constructorLevel.Value.i] : default;
            if (levelValue.Value != null && SymbolEqualityComparer.Default.Equals(levelValue.Type, level) &&
                (Convert.ToInt32(levelValue.Value) < 2 || Convert.ToInt32(levelValue.Value) > 5))
            {
                continue;
            }

            var id = data.NamedArguments.FirstOrDefault(pair => pair.Key == "EventId");
            var constructorId = data.AttributeConstructor?.Parameters
                .Select((p, i) => (p, i)).FirstOrDefault(pair => pair.p.Name == "eventId");
            var idValue = id.Key == "EventId" ? id.Value :
                constructorId.HasValue && constructorId.Value.p != null &&
                constructorId.Value.i < data.ConstructorArguments.Length
                    ? data.ConstructorArguments[constructorId.Value.i] : default;
            if (!(idValue.Value is int number && number != 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(EventIdRule, syntax.GetLocation()));
            }

            var text = data.NamedArguments.FirstOrDefault(pair => pair.Key == "Message");
            var constructorMessage = data.AttributeConstructor?.Parameters
                .Select((p, i) => (p, i)).FirstOrDefault(pair => pair.p.Name == "message");
            var value = text.Key == "Message" ? text.Value :
                constructorMessage.HasValue && constructorMessage.Value.p != null &&
                constructorMessage.Value.i < data.ConstructorArguments.Length
                    ? data.ConstructorArguments[constructorMessage.Value.i] : default;
            if (!(value.Value is string template && IsStructured(template)) ||
                syntax.DescendantNodesAndSelf().OfType<InterpolatedStringExpressionSyntax>().Any())
            {
                context.ReportDiagnostic(Diagnostic.Create(TemplateRule, syntax.GetLocation()));
            }
        }
    }

    private static bool IsNamedLevel(string name) =>
        name == "LogInformation" || name == "LogWarning" ||
        name == "LogError" || name == "LogCritical";

    private static IArgumentOperation? Argument(IInvocationOperation invocation, string name,
        ITypeSymbol type) => invocation.Arguments.FirstOrDefault(a =>
            a.Parameter?.Name == name && SymbolEqualityComparer.Default.Equals(a.Parameter.Type, type));

    private static IArgumentOperation? Argument(IInvocationOperation invocation, string name,
        SpecialType type) => invocation.Arguments.FirstOrDefault(a =>
            a.Parameter?.Name == name && a.Parameter.Type.SpecialType == type);

    private static IArgumentOperation? Argument(IInvocationOperation invocation, IParameterSymbol parameter) =>
        invocation.Arguments.FirstOrDefault(a =>
            SymbolEqualityComparer.Default.Equals(a.Parameter?.OriginalDefinition, parameter.OriginalDefinition));

    private static bool RequiresPolicy(IOperation? value, INamedTypeSymbol level)
    {
        if (value == null || !SymbolEqualityComparer.Default.Equals(value.Type, level))
        {
            return true;
        }

        return !TryInt(value.ConstantValue, out var number) || number >= 2 && number <= 5;
    }

    private static bool ValidEventId(IOperation? value, INamedTypeSymbol eventId)
    {
        if (value == null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(value.Type, eventId))
        {
            if (value is IConversionOperation conversion && conversion.OperatorMethod?.Name == "op_Implicit")
            {
                return TryInt(conversion.Operand.ConstantValue, out var implicitId) && implicitId != 0;
            }

            if (value is IObjectCreationOperation creation &&
                SymbolEqualityComparer.Default.Equals(creation.Constructor?.ContainingType, eventId))
            {
                var id = creation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "id");
                return id != null && TryInt(id.Value.ConstantValue, out var numericId) && numericId != 0;
            }
        }

        return false;
    }

    private static bool TryInt(Optional<object?> constant, out int result)
    {
        result = 0;
        if (!constant.HasValue || constant.Value == null)
        {
            return false;
        }

        try
        {
            result = Convert.ToInt32(constant.Value);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool ValidTemplate(IOperation? value) =>
        value?.ConstantValue.HasValue == true &&
        value.ConstantValue.Value is string text &&
        !value.Syntax.DescendantNodesAndSelf().OfType<InterpolatedStringExpressionSyntax>().Any() &&
        IsStructured(text);

    private static bool IsStructured(string template)
    {
        if (template.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    i++;
                    continue;
                }

                return false;
            }

            if (template[i] != '{')
            {
                continue;
            }

            if (i + 1 < template.Length && template[i + 1] == '{')
            {
                i++;
                continue;
            }

            var start = ++i;
            if (i >= template.Length || !(char.IsLetter(template[i]) || template[i] == '_'))
            {
                return false;
            }

            while (i < template.Length && (char.IsLetterOrDigit(template[i]) ||
                template[i] == '_' || template[i] == '.'))
            {
                i++;
            }

            if (i == start || i >= template.Length ||
                template[i] != '}' && template[i] != ',' && template[i] != ':')
            {
                return false;
            }

            if (template[i] != '}')
            {
                i = template.IndexOf('}', i + 1);
                if (i < 0)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
