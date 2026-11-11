using System;
using Xunit;

namespace Mauve.Governance.Testing.XunitV3;

/// <summary>Assigns a requirement of the owning feature to an xUnit v3 test method.</summary>
/// <example><code>
/// [Fact]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// [Requirement("FEAT-Checkout-002")]
/// public void Checkout() { }
/// </code></example>
/// <remarks>Creates a requirement trait.</remarks>
/// <param name="id">A distinct requirement identifier of the owning feature.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirementAttribute(string id) : TraitAttribute(Metadata.Requirement, id)
{
}
