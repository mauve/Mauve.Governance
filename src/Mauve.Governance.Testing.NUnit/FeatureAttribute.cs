using System;
using NUnit.Framework;

namespace Mauve.Governance.Testing.NUnit;

/// <summary>Assigns one owning feature to an NUnit test method as a native property.</summary>
/// <example><code>
/// [Test]
/// [Feature("FEAT-Checkout")]
/// [Requirement("FEAT-Checkout-001")]
/// public void Checkout() { }
/// </code></example>
/// <param name="id">The feature identifier, for example <c>FEAT-Checkout</c>.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class FeatureAttribute(string id) : PropertyAttribute(Metadata.Feature, id)
{
}
