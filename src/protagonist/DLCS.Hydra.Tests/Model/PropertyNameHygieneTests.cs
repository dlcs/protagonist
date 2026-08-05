using System.Reflection;
using DLCS.HydraModel;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace DLCS.Hydra.Tests.Model;

public class PropertyNameHygieneTests
{
    [Fact]
    public void No_JsonProperty_name_has_leading_or_trailing_whitespace()
    {
        var assemblies = new[] { typeof(Customer).Assembly, typeof(global::Hydra.Model.Class).Assembly };

        var offenders = assemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(p => new { p.DeclaringType, p.Name, Attr = p.GetCustomAttribute<JsonPropertyAttribute>() })
            .Where(x => x.Attr?.PropertyName != null && x.Attr.PropertyName != x.Attr.PropertyName.Trim())
            .Select(x => $"{x.DeclaringType!.FullName}.{x.Name}: \"{x.Attr!.PropertyName}\"")
            .ToList();

        offenders.Should().BeEmpty(
            "Hydra wire property names must not carry leading or trailing whitespace - clients would have to " +
            "request the exact spaced key, e.g. obj[\"created \"]");
    }
}
