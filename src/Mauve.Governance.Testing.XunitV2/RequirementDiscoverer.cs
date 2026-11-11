using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Mauve.Governance.Testing.XunitV2;

/// <summary>Publishes <see cref="RequirementAttribute"/> as an xUnit v2 requirement trait.</summary>
public sealed class RequirementDiscoverer : ITraitDiscoverer
{
    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var values = traitAttribute.GetConstructorArguments();
        yield return new KeyValuePair<string, string>(Metadata.Requirement, values.FirstOrDefault()?.ToString() ?? "");
    }
}
