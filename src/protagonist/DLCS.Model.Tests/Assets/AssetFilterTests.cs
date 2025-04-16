using API.Converters;
using Microsoft.AspNetCore.Http;

namespace DLCS.Model.Tests.Assets;

public class AssetFilterTests
{
    [Fact]
    public void Obtain_AssetFilter_From_Q_Param_Directly()
    {
        // arrange
        var q = @"{""string1"":""s1"",""string2"":""s2"",""number3"":3,""space"":99,""manifests"":[""first""]}";
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?q=" + q);
        
        // act
        var filter = httpRequest.GetAssetFilterFromQParam(q);
        
        // assert
        filter.Should().NotBeNull();
        filter.Reference1.Should().Be("s1");
        filter.Reference2.Should().Be("s2");
        filter.Reference3.Should().BeNull();
        filter.NumberReference1.Should().BeNull();
        filter.NumberReference3.Should().Be(3);
        filter.Space.Should().Be(99);
        filter.Manifests.Should().BeEquivalentTo("first");
    }
    
    [Fact]
    public void Parse_AssetFilter_From_Request()
    {
        // arrange
        var q = @"{""string3"":""s3"",""number1"":1}";
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?q=" + q);
        
        // act
        var filter = httpRequest.GetAssetFilterFromQParam();
        
        // assert
        filter.Should().NotBeNull();
        filter.Reference1.Should().Be(null);
        filter.Reference3.Should().Be("s3");
        filter.NumberReference1.Should().Be(1);
        filter.NumberReference3.Should().BeNull();
        filter.Space.Should().BeNull();
    }

    [Fact]
    public void Construct_AssetFilter_From_Specific_Params()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?string1=s1&string2=s2&string3=s3&number1=1&number2=2&number3=3&manifests=first,second");
        
        // act
        var filter = httpRequest.UpdateAssetFilterFromQueryStringParams(null);
        
        // assert
        filter.Should().NotBeNull();
        filter.Reference1.Should().Be("s1");
        filter.Reference2.Should().Be("s2");
        filter.Reference3.Should().Be("s3");
        filter.NumberReference1.Should().Be(1);
        filter.NumberReference2.Should().Be(2);
        filter.NumberReference3.Should().Be(3);
        filter.Manifests.Should().BeEquivalentTo("first", "second");
        filter.Space.Should().BeNull();
    }
    
    
    [Fact]
    public void Update_AssetFilter_From_Specific_Params()
    {
        var q = @"{""string3"":""s3"",""number1"":1}";
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?q=" + q + "&string1=s1&string3=s3updated&number3=3");
        
        // act
        var filter = httpRequest.GetAssetFilterFromQParam();
        filter = httpRequest.UpdateAssetFilterFromQueryStringParams(filter);
        
        // assert
        filter.Should().NotBeNull();
        filter.Reference1.Should().Be("s1");
        filter.Reference2.Should().BeNull();
        filter.Reference3.Should().Be("s3updated");
        filter.NumberReference1.Should().Be(1);
        filter.NumberReference2.Should().BeNull();
        filter.NumberReference3.Should().Be(3);
        filter.Space.Should().BeNull();
    }
    
}
