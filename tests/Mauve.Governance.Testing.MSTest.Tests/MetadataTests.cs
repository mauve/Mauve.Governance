using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mauve.Governance.Testing.MSTest;

namespace Mauve.Governance.Testing.MSTest.Tests;

[TestClass]
public sealed class MetadataTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestProperty(Metadata.Feature, "FEAT-Payments")]
    [TestProperty(Metadata.Requirement, "FEAT-Payments-01")]
    public void NativeMetadataIsDiscoverable()
    {
        Console.Write("governance stdout <&> fixture");
        Console.Error.Write("governance stderr <&> fixture");
        var attachment = Path.Combine(TestContext.TestRunDirectory!, "governance-attachment.txt");
        File.WriteAllText(attachment, "governance attachment <&> fixture");
        TestContext.AddResultFile(attachment);
        Assert.IsNotNull(typeof(MetadataTests).GetMethod(nameof(NativeMetadataIsDiscoverable)));
    }

    [TestMethod]
    [DataRow(1)]
    [TestProperty(Metadata.Feature, "FEAT-Payments")]
    [TestProperty(Metadata.Requirement, "FEAT-Payments-02")]
    public void ParameterizedMetadataIsDiscoverable(int value) => Assert.AreEqual(1, value);

}
