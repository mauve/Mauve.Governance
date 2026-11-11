using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Mauve.Governance.Testing.XunitV2;

/// <summary>Publishes <see cref="FeatureAttribute"/> as an xUnit v2 feature trait.</summary>
public sealed class FeatureDiscoverer : ITraitDiscoverer
{
    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var values = traitAttribute.GetConstructorArguments();
        yield return new KeyValuePair<string, string>(Metadata.Feature, values.FirstOrDefault()?.ToString() ?? "");
    }
}
