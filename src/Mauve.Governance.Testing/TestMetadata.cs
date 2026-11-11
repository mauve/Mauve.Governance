namespace Mauve.Governance.Testing;

/// <summary>Property and trait names shared by the supported test frameworks.</summary>
public static class TestMetadata
{
    /// <summary>The property name identifying a test's single owning feature.</summary>
    /// <example>With MSTest: <code>
    /// [TestMethod]
    /// [TestProperty(TestMetadata.Feature, "FEAT-Checkout")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Feature = "Feature";

    /// <summary>The property name identifying a requirement owned by the test's feature.</summary>
    /// <example>With MSTest: <code>
    /// [TestMethod]
    /// [TestProperty(TestMetadata.Feature, "FEAT-Checkout")]
    /// [TestProperty(TestMetadata.Requirement, "FEAT-Checkout-001")]
    /// public void Checkout() { }
    /// </code></example>
    public const string Requirement = "Requirement";
}
