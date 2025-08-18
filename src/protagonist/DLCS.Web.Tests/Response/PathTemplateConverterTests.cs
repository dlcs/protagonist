using System;
using System.Collections.Generic;
using System.ComponentModel;
using DLCS.Web.Response;

namespace DLCS.Web.Tests.Response;

public class PathTemplateConverterTests
{
    private readonly TypeConverter pathTemplateConverter = TypeDescriptor.GetConverter(typeof(PathTemplate));

    [Fact]
    public void CanConvertFrom_True_ForString()
        => pathTemplateConverter.CanConvertFrom(typeof(string)).Should().BeTrue("Conversion from string supported");

    [Theory]
    [InlineData(typeof(PathTemplate))]
    [InlineData(typeof(int))]
    [InlineData(typeof(Dictionary<string, string>))]
    public void CanConvertFrom_False_ForNonString(Type type)
        => pathTemplateConverter.CanConvertFrom(type).Should().BeFalse("Conversion from non-string not supported");

    [Fact]
    public void ConvertFrom_String_SetsPath()
    {
        const string path = "/path/{space}";
        var result = pathTemplateConverter.ConvertFrom(path) as PathTemplate;
        
        result.Path.Should().Be(path, "Path value set to provided string");
        result.PrefixReplacements.Should().BeEmpty("No prefix replacements set but defaulted to empty dict");
    }
    
    [Fact]
    public void ConvertTo_String_ReturnsPath()
    {
        const string path = "/path/{space}";
        var pathTemplate = new PathTemplate
        {
            Path = path,
            PrefixReplacements = new() { ["space"] = "s" },
        };
        
        var result = pathTemplateConverter.ConvertTo(pathTemplate, typeof(string));
        
        result.ToString().Should().Be(path, "Path value returned");
    }
}
