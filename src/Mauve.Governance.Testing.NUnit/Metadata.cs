namespace Mauve.Governance.Testing.NUnit;

/// <summary>Names for NUnit's native <c>Property</c> annotations.</summary>
public static class Metadata
{
    /// <summary>The name of the owning feature property.</summary>
    /// <example><code>
    /// [Test]
    /// [Property(Metadata.Feature, "FEAT-Checkout")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Feature = TestMetadata.Feature;

    /// <summary>The name of a requirement property; repeat it for distinct requirements.</summary>
    /// <example><code>
    /// [Test]
    /// [Property(Metadata.Feature, "FEAT-Checkout")]
    /// [Property(Metadata.Requirement, "FEAT-Checkout-001")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Requirement = TestMetadata.Requirement;
}
