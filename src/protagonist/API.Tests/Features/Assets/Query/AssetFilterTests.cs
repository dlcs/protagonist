using API.Features.Assets.Query;
using Microsoft.AspNetCore.Http;

namespace API.Tests.Features.Assets.Query;

public class AssetFilterTests
{
    [Fact]
    public void GetAssetQuery_MapsQParameter()
    {
        // arrange
        const string q = @"{""string1"":""s1"",""string2"":""s2"",""number3"":3,""space"":99,""manifests"":[""first""]}";
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString($"?q={q}");
        
        // act
        var assetQueryModel = httpRequest.GetAssetQuery();
        
        // assert
        var filter = assetQueryModel.Filter;
        filter.Should().NotBeNull();
        filter!.Reference1.Should().Be("s1");
        filter.Reference2.Should().Be("s2");
        filter.Reference3.Should().BeNull();
        filter.NumberReference1.Should().BeNull();
        filter.NumberReference3.Should().Be(3);
        filter.Space.Should().Be(99);
        filter.Manifests.Should().BeEquivalentTo("first");
    }

    [Fact]
    public void GetAssetQuery_MapsDirectParameter()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?string1=s1&string2=s2&string3=s3&number1=1&number2=2&number3=3&manifests=first,second");
        
        // act
        var assetQueryModel = httpRequest.GetAssetQuery();
        
        // assert
        var filter = assetQueryModel.Filter;
        filter!.Reference1.Should().Be("s1");
        filter.Reference2.Should().Be("s2");
        filter.Reference3.Should().Be("s3");
        filter.NumberReference1.Should().Be(1);
        filter.NumberReference2.Should().Be(2);
        filter.NumberReference3.Should().Be(3);
        filter.Manifests.Should().BeEquivalentTo("first", "second");
        filter.Space.Should().BeNull();
    }
    
    [Fact]
    public void GetAssetQuery_DirectParameter_OverridesQParam()
    {
        const string q = @"{""string3"":""s3"",""number1"":1,""manifests"":[""first""]}";
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString($"?q={q}&string1=s1&string3=s3updated&number3=3&manifests=second");
        
        // act
        var assetQueryModel = httpRequest.GetAssetQuery();
        
        // assert
        var filter = assetQueryModel.Filter;
        filter!.Reference1.Should().Be("s1");
        filter.Reference2.Should().BeNull();
        filter.Reference3.Should().Be("s3updated");
        filter.NumberReference1.Should().Be(1);
        filter.NumberReference2.Should().BeNull();
        filter.NumberReference3.Should().Be(3);
        filter.Manifests.Should().BeEquivalentTo("second");
        filter.Space.Should().BeNull();
    }

    [Fact]
    public void GetAssetQuery_MapsKnownInclude()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?include=adjuncts");
        
        var assetQueryModel = httpRequest.GetAssetQuery();
        
        var include = assetQueryModel.Include;
        include.Should().NotBeNull();
        include!.Include.Should().BeEquivalentTo(IncludeFields.Adjuncts);
    }
    
    [Fact]
    public void GetAssetQuery_IgnoresUnknownInclude()
    {
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.QueryString = new QueryString("?include=foo,bar,baz");
        
        var assetQueryModel = httpRequest.GetAssetQuery();
        
        var include = assetQueryModel.Include;
        include.Should().NotBeNull();
        include!.Include.Should().BeNull();
    }
}
