using System;
using Xunit.Sdk;

namespace Mauve.Governance.Testing.XunitV2;

/// <summary>Assigns a requirement of the owning feature to an xUnit v2 test method.</summary>
/// <example><code>
/// [Fact]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// [Requirement("FEAT-Checkout-002")]
/// public void Checkout() { }
/// </code></example>
/// <remarks>Creates a requirement trait.</remarks>
/// <param name="id">A distinct requirement identifier of the owning feature.</param>
[TraitDiscoverer("Mauve.Governance.Testing.XunitV2.RequirementDiscoverer", "Mauve.Governance.Testing.XunitV2")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirementAttribute(string id) : Attribute, ITraitAttribute
{
    /// <summary>
    /// Gets the requirement identifier.
    /// </summary>
    public string Id { get; } = id;
}
