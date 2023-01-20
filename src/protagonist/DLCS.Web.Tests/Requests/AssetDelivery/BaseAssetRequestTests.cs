using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FluentAssertions;
using Xunit;

namespace DLCS.Web.Tests.Requests.AssetDelivery;

public class BaseAssetRequestTests
{
    [Fact]
    public void GetAssetImageId_Correct()
    {
        // Arrange
        var baseRequest = new BaseAssetRequest
        {
            Customer = new CustomerPathElement(1234, "Ultrasonic"),
            Space = 4,
            AssetId = "my-asset"
        };
        
        // Act
        var assetImageId = baseRequest.GetAssetId();
        
        // Assert
        assetImageId.Customer.Should().Be(1234);
        assetImageId.Space.Should().Be(4);
        assetImageId.Asset.Should().Be("my-asset");
    }

    [Fact]
    public void CloneBasicPathElements_CreatesClone()
    {
        // Arrange
        var baseRequest = new BasicPathElements
        {
            CustomerPathValue = "1234",
            Space = 4,
            AssetPath = "10/10/consideration",
            VersionPathValue = "v9",
            RoutePrefix = "iiif-img",
        };
        
        // Act
        var clone = baseRequest.CloneBasicPathElements();
        
        // Assert
        clone.Should().NotBe(baseRequest);
        clone.CustomerPathValue.Should().Be(baseRequest.CustomerPathValue);
        clone.Space.Should().Be(baseRequest.Space);
        clone.AssetPath.Should().Be(baseRequest.AssetPath);
        clone.RoutePrefix.Should().Be(baseRequest.RoutePrefix);
        clone.VersionPathValue.Should().Be(baseRequest.VersionPathValue);
    }
}