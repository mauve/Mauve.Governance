using System;
using NUnit.Framework;

namespace Mauve.Governance.Testing.NUnit;

/// <summary>Assigns a requirement of the owning feature to an NUnit test method.</summary>
/// <example><code>
/// [Test]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// [Requirement("FEAT-Checkout-002")]
/// public void Checkout() { }
/// </code></example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirementAttribute : PropertyAttribute
{
    /// <summary>Creates a native NUnit requirement property.</summary>
    /// <param name="id">A distinct requirement identifier of the owning feature.</param>
    public RequirementAttribute(string id) : base(Metadata.Requirement, id) { }
}
