using System;
using System.IO;
using System.Linq;
using Mauve.Governance.Testing.TrxLogger;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Xunit;

namespace Mauve.Governance.Testing.TrxLogger.Tests;

public sealed class GovernanceTrxLoggerTests
{
    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Rejects_more_than_bounded_result_count()
    {
        var logger = new GovernanceTrxLogger();
        var result = Result();
        for (var index = 0; index < GovernanceTrxLogger.MaximumResults; index++)
        {
            logger.Record(result);
        }

        Assert.StartsWith("MG-GOV-TRX-0001:", Assert.Throws<InvalidDataException>(
            () => logger.Record(result)).Message);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Rejects_large_or_excessive_metadata()
    {
        var logger = new GovernanceTrxLogger();
        var oversized = Result();
        oversized.TestCase.Traits.Add(new Trait("Requirement",
            new string('x', GovernanceTrxLogger.MaximumFieldLength + 1)));
        Assert.StartsWith("MG-GOV-TRX-0002:", Assert.Throws<InvalidDataException>(
            () => logger.Record(oversized)).Message);

        var excessive = Result();
        for (var index = 0; index < GovernanceTrxLogger.MaximumTraits; index++)
        {
            excessive.TestCase.Traits.Add(new Trait("Other", index.ToString()));
        }
        Assert.StartsWith("MG-GOV-TRX-0002:", Assert.Throws<InvalidDataException>(
            () => logger.Record(excessive)).Message);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Rejects_conflicting_traits_for_same_id_but_allows_order_only_difference()
    {
        var logger = new GovernanceTrxLogger();
        var first = Result();
        first.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-003"));
        logger.Record(first);

        var reordered = Result(includeFeature: false);
        reordered.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-003"));
        reordered.TestCase.Traits.Add(new Trait("Feature", "FEAT-03"));
        logger.Record(reordered);

        var changed = Result();
        changed.TestCase.Traits.Add(new Trait("Requirement", "FEAT-03-004"));
        Assert.StartsWith("MG-GOV-TRX-0003:", Assert.Throws<InvalidDataException>(
            () => logger.Record(changed)).Message);
    }

    [Fact]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Rejects_aggregate_size_and_never_retains_test_result_objects()
    {
        var logger = new GovernanceTrxLogger();
        var result = Result();
        result.TestCase.DisplayName = new string('n', GovernanceTrxLogger.MaximumFieldLength);
        result.Messages.Add(new TestResultMessage("StdOut", new string('o', 100_000)));
        for (var index = 0; index < GovernanceTrxLogger.MaximumResults; index++)
        {
            try
            {
                logger.Record(result);
            }
            catch (InvalidDataException error)
            {
                Assert.StartsWith("MG-GOV-TRX-0004:", error.Message);
                Assert.DoesNotContain(typeof(TestResult), typeof(GovernanceTrxLogger)
                    .GetFields(System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)
                    .SelectMany(field => new[] { field.FieldType }
                        .Concat(field.FieldType.GetGenericArguments())));
                return;
            }
        }
        throw new Xunit.Sdk.XunitException("The retained-metadata size limit was not enforced.");
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
}
