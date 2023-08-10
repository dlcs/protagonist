using System;
using System.Collections.Generic;
using DLCS.Web.Response;

namespace DLCS.Web.Tests.Response;

public class TypedPathTemplateOptionsTests
{
    private readonly TypedPathTemplateOptions sut;

    public TypedPathTemplateOptionsTests()
    {
        sut = new TypedPathTemplateOptions
        {
            Defaults = new Dictionary<string, string>
            {
                ["Type1"] = "/path/type1",
                ["Type2"] = "/path/type2"
            },
            Overrides = new Dictionary<string, Dictionary<string, string>>
            {
                ["proxy.host"] = new()
                {
                    ["Type1"] = "/different/type1"
                }
            }
        };
    }

    [Fact]
    public void GetPathTemplateForHostAndType_Throws_IfNoDefaultForType()
    {
        // Act
        Action action = () => sut.GetPathTemplateForHostAndType("default.host", "Type3");
        
        // Assert
        action.Should()
            .Throw<KeyNotFoundException>()
            .WithMessage("Could not find default path template for type: Type3");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsDefault_IfNoHostOverride()
    {
        // Arrange
        const string expected = "/path/type1";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("default.host", "Type1");
        
        // Assert
        actual.Should().Be(expected, "default is returned if no override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsDefault_IfHostEntry_ButNoServiceOverride()
    {
        // Arrange
        const string expected = "/path/type2";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type2");
        
        // Assert
        actual.Should().Be(expected, "default is returned as no host-specific override found");
    }
    
    [Fact]
    public void GetPathTemplateForHostAndType_ReturnsOverride_IfFound()
    {
        // Arrange
        const string expected = "/different/type1";
        
        // Act
        var actual = sut.GetPathTemplateForHostAndType("proxy.host", "Type1");
        
        // Assert
        actual.Should().Be(expected, "type override is returned");
    }
}