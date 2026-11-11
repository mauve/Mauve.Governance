#if GOVERNANCE_OUTCOME_FIXTURE
using Xunit;

namespace Mauve.Governance.Testing.XunitV2.Tests;

public sealed class OutcomeFixtures
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Parameterized_pass_and_fail(bool shouldFail) =>
        Assert.False(shouldFail);

    [Fact(Skip = "Intentional skip for Governance TRX verification")]
    [Trait("Feature", "FEAT-03")]
    [Trait("Requirement", "FEAT-03-003")]
    public void Explicitly_skipped() =>
        Assert.True(false);
}
#endif
