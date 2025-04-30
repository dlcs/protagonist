using DLCS.Web.Response;

namespace DLCS.Web.Tests.Response;

public class PathTemplateTests
{
    [Fact]
    public void GetPrefixForPath_ReturnsProvidedPrefix_IfNoReplacement()
    {
        const string prefix = "iiif-img";
        var pathTemplate = new PathTemplate { Path = "/path" };
        pathTemplate.GetPrefixForPath(prefix).Should().Be(prefix, "No replacement found");
    }
    
    [Fact]
    public void GetPrefixForPath_ReturnsReplacement_IfFound()
    {
        const string prefix = "iiif-img";
        const string replacement = "foo";
        var pathTemplate = new PathTemplate { Path = "/path" };
        pathTemplate.PrefixReplacements.Add(prefix, "foo");
        pathTemplate.GetPrefixForPath(prefix).Should().Be(replacement, "No replacement found");
    }
}
