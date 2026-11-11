using Mauve.Governance.Testing.NUnit;
using NUnit.Framework;

namespace Mauve.Governance.Testing.NUnit.Tests;

public sealed class MetadataTests
{
    [Test]
    [Property(Metadata.Feature, "FEAT-Payments")]
    [Property(Metadata.Requirement, "FEAT-Payments-01")]
    public void NativeMetadataIsDiscoverable() => Assert.That(true, Is.True);

    [TestCase(1)]
    [Property(Metadata.Feature, "FEAT-Payments")]
    [Property(Metadata.Requirement, "FEAT-Payments-02")]
    public void ParameterizedMetadataIsDiscoverable(int value) => Assert.That(value, Is.EqualTo(1));

    [Test]
    [Feature("FEAT-Payments")]
    [Requirement("FEAT-Payments-03")]
    [Requirement("FEAT-Payments-04")]
    public void ConvenienceMetadataIsDiscoverable() => Assert.That(true, Is.True);
}
