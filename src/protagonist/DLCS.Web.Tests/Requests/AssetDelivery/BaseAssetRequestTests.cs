using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FluentAssertions;
using Xunit;

namespace DLCS.Web.Tests.Requests.AssetDelivery
{
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
    }
}