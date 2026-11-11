namespace Mauve.Governance.Testing.XunitV2;

/// <summary>Names for xUnit v2's native <c>Trait</c> annotations.</summary>
public static class Metadata
{
    /// <summary>The name of the owning feature trait.</summary>
    /// <example><code>
    /// [Fact]
    /// [Trait(Metadata.Feature, "FEAT-Checkout")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Feature = TestMetadata.Feature;

    /// <summary>The name of a requirement trait; repeat it for distinct requirements.</summary>
    /// <example><code>
    /// [Fact]
    /// [Trait(Metadata.Feature, "FEAT-Checkout")]
    /// [Trait(Metadata.Requirement, "FEAT-Checkout-001")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Requirement = TestMetadata.Requirement;
}
