using FluentAssertions;
using Hydra;
using Hydra.Model;
using Xunit;

namespace DLCS.Hydra.Tests.JsonLd;

public class HydraJsonTests
{
    private const string HydraContext = "http://www.w3.org/ns/hydra/context.jsonld";
    
    [Fact]
    public void HydraClass_Has_HydraType()
    {
        var operation = new Operation();
        operation.Type.Should().Be("hydra:Operation");
    }
    
    
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, HydraContext)]
    public void HydraClass_Has_HydraContext(bool requireContext, string? expectedContext)
    {
        var operation = new Operation
        {
            WithContext = requireContext
        };
        operation.Context.Should().Be(expectedContext);
    }
    
    
    [Fact]
    public void HydraContext_Not_Overwritten()
    {
        var operation = new Operation
        {
            Context = "test"
        };
        operation.Context.Should().Be("test");
    }

    [Fact]
    public void HydraId_GetLastPathElement_IsString()
    {
        var operation = new Operation
        {
            Id = "https://example.org/api/path-part/1"
        };
        operation.Id.GetLastPathElement().Should().Be("1");
    }
    
    
    [Fact]
    public void HydraId_GetLastPathElement_IsInt()
    {
        var operation = new Operation
        {
            Id = "https://example.org/api/path-part/1"
        };
        operation.Id.GetLastPathElementAsInt().Should().Be(1);
    }
    
    
    [Fact]
    public void HydraId_GetLastPathElementWithPrefix_ReturnsNull()
    {
        var operation = new Operation
        {
            Id = "https://example.org/api/path-part/1"
        };
        operation.Id.GetLastPathElement("some-path").Should().BeNull();
    }
    
    [Fact]
    public void HydraId_GetLastPathElementWithPrefix_ReturnsString()
    {
        var operation = new Operation
        {
            Id = "https://example.org/api/path-part/1"
        };
        operation.Id.GetLastPathElement("api/path-part/").Should().Be("1");
        
    }


    [Fact] 
    public void HydraId_GetLastPathElementWithPrefixNotExactMatch_ReturnsNull()
    {
        var operation = new Operation
        {
            Id = "https://example.org/api/path-part/1"
        };
        operation.Id.GetLastPathElement("api/path-part").Should().BeNull();
    }

    [Fact]
    public void GetLastPathElementAsInt_Throws_InvalidCastException()
    {
        var path = "foo/bar";
        Action action = () => path.GetLastPathElementAsInt();
        action.Should().Throw<FormatException>();
    }

    
    
    
}