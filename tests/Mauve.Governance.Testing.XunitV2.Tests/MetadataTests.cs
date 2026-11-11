using Mauve.Governance.Testing.XunitV2;
using Xunit;

namespace Mauve.Governance.Testing.XunitV2.Tests;

public sealed class MetadataTests
{
    [Fact]
    [Trait(Metadata.Feature, "FEAT-Payments")]
    [Trait(Metadata.Requirement, "FEAT-Payments-01")]
    public void NativeMetadataIsDiscoverable() => Assert.True(true);

    [Theory]
    [InlineData(1)]
    [Trait(Metadata.Feature, "FEAT-Payments")]
    [Trait(Metadata.Requirement, "FEAT-Payments-02")]
    public void ParameterizedMetadataIsDiscoverable(int value) => Assert.Equal(1, value);

    [Fact]
    [Feature("FEAT-Payments")]
    [Requirement("FEAT-Payments-03")]
    [Requirement("FEAT-Payments-04")]
    public void ConvenienceMetadataIsDiscoverable() => Assert.True(true);
}
