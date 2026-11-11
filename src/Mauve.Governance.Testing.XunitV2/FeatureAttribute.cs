using System;
using Xunit.Sdk;

namespace Mauve.Governance.Testing.XunitV2;

/// <summary>Assigns one owning feature to an xUnit v2 test method as a trait.</summary>
/// <example><code>
/// [Fact]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// public void Checkout() { }
/// </code></example>
/// <remarks>Creates the owning feature trait.</remarks>
/// <param name="id">The feature identifier, for example <c>FEAT-Checkout</c>.</param>
[TraitDiscoverer("Mauve.Governance.Testing.XunitV2.FeatureDiscoverer", "Mauve.Governance.Testing.XunitV2")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FeatureAttribute(string id) : Attribute, ITraitAttribute
{
    /// <summary>
    /// Gets the feature identifier.
    /// </summary>
    public string Id { get; } = id;
}
