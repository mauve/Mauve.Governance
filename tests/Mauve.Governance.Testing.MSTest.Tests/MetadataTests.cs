using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mauve.Governance.Testing.MSTest;

namespace Mauve.Governance.Testing.MSTest.Tests;

[TestClass]
public sealed class MetadataTests
{
    [TestMethod]
    [TestProperty(Metadata.Feature, "FEAT-Payments")]
    [TestProperty(Metadata.Requirement, "FEAT-Payments-01")]
    public void NativeMetadataIsDiscoverable() =>
        Assert.IsNotNull(typeof(MetadataTests).GetMethod(nameof(NativeMetadataIsDiscoverable)));

    [TestMethod]
    [DataRow(1)]
    [TestProperty(Metadata.Feature, "FEAT-Payments")]
    [TestProperty(Metadata.Requirement, "FEAT-Payments-02")]
    public void ParameterizedMetadataIsDiscoverable(int value) => Assert.AreEqual(1, value);

}
