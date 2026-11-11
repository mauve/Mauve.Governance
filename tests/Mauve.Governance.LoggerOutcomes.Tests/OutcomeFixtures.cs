using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mauve.Governance.LoggerOutcomes.Tests;

[TestClass]
public sealed class OutcomeFixtures
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [TestProperty("Feature", "FEAT-03")]
    [TestProperty("Requirement", "FEAT-03-003")]
    public void Parameterized_pass_and_fail(bool shouldFail)
    {
        Assert.IsFalse(shouldFail, "Intentional failure to verify observed TRX outcomes.");
    }

    [TestMethod]
    [Ignore]
    [TestProperty("Feature", "FEAT-03")]
    [TestProperty("Requirement", "FEAT-03-003")]
    public void Explicitly_skipped()
    {
        Assert.Fail("An ignored test must not execute.");
    }
}
