namespace Mauve.Governance.Testing.MSTest;

/// <summary>Names for MSTest's native <c>TestProperty</c> annotations.</summary>
public static class Metadata
{
    /// <summary>The name of the owning feature test property.</summary>
    /// <example><code>
    /// [TestMethod]
    /// [TestProperty(Metadata.Feature, "FEAT-Checkout")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Feature = TestMetadata.Feature;

    /// <summary>The name of a requirement test property; repeat it for distinct requirements.</summary>
    /// <example><code>
    /// [TestMethod]
    /// [TestProperty(Metadata.Feature, "FEAT-Checkout")]
    /// [TestProperty(Metadata.Requirement, "FEAT-Checkout-001")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Requirement = TestMetadata.Requirement;
}
