using System;
using Xunit;

namespace Mauve.Governance.Testing.XunitV3;

/// <summary>Assigns one owning feature to an xUnit v3 test method as a trait.</summary>
/// <example><code>
/// [Fact]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// public void Checkout() { }
/// </code></example>
/// <remarks>Creates the owning feature trait.</remarks>
/// <param name="id">The feature identifier, for example <c>FEAT-Checkout</c>.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FeatureAttribute(string id) : TraitAttribute(Metadata.Feature, id)
{
}
