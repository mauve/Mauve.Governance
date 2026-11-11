#if GOVERNANCE_OUTCOME_FIXTURE
using NUnit.Framework;

namespace Mauve.Governance.Testing.NUnit.Tests;

public sealed class OutcomeFixtures
{
    [TestCase(false)]
    [TestCase(true)]
    [Property("Feature", "FEAT-03")]
    [Property("Requirement", "FEAT-03-003")]
    public void Parameterized_pass_and_fail(bool shouldFail) =>
        Assert.That(shouldFail, Is.False);

    [Test]
    [Ignore("Intentional skip for Governance TRX verification")]
    [Property("Feature", "FEAT-03")]
    [Property("Requirement", "FEAT-03-003")]
    public void Explicitly_skipped() =>
        Assert.Fail("An ignored test must not execute.");
}
#endif
