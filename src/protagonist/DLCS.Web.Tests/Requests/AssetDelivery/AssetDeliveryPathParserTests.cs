using System;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace DLCS.Web.Tests.Requests.AssetDelivery;

public class AssetDeliveryPathParserTests
{
    private readonly IPathCustomerRepository pathCustomerRepository;
    private readonly AssetDeliveryPathParser sut;

    public AssetDeliveryPathParserTests()
    {
        pathCustomerRepository = A.Fake<IPathCustomerRepository>();
        sut = new AssetDeliveryPathParser(pathCustomerRepository);
    }

    [Fact]
    public async Task Parse_ImageRequest_WithCustomerId_ReturnsCorrectRequest()
    {
        // Arrange
        const string path = "/thumbs/99/1/the-astronaut";
        var customer = new CustomerPathElement(99, "Test-Customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("99"))
            .Returns(customer);

        // Act
        var thumbnailRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);
        
        // Assert
        thumbnailRequest.RoutePrefix.Should().Be("thumbs");
        thumbnailRequest.VersionedRoutePrefix.Should().Be("thumbs");
        thumbnailRequest.VersionPathValue.Should().BeNull();
        thumbnailRequest.CustomerPathValue.Should().Be("99");
        thumbnailRequest.Customer.Should().Be(customer);
        thumbnailRequest.BasePath.Should().Be("/thumbs/99/1/");
        thumbnailRequest.Space.Should().Be(1);
        thumbnailRequest.AssetPath.Should().Be("the-astronaut");
        thumbnailRequest.AssetId.Should().Be("the-astronaut");
    }

    [Fact]
    public async Task Parse_ImageRequest_WithCustomerName_ReturnsCorrectRequest()
    {
        // Arrange
        const string path = "/thumbs/test-customer/1/the-astronaut";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("test-customer"))
            .Returns(customer);

        // Act
        var thumbnailRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        thumbnailRequest.RoutePrefix.Should().Be("thumbs");
        thumbnailRequest.VersionedRoutePrefix.Should().Be("thumbs");
        thumbnailRequest.VersionPathValue.Should().BeNull();
        thumbnailRequest.CustomerPathValue.Should().Be("test-customer");
        thumbnailRequest.Customer.Should().Be(customer);
        thumbnailRequest.BasePath.Should().Be("/thumbs/test-customer/1/");
        thumbnailRequest.Space.Should().Be(1);
        thumbnailRequest.AssetPath.Should().Be("the-astronaut");
        thumbnailRequest.AssetId.Should().Be("the-astronaut");
    }
    
    [Fact]
    public async Task Parse_ImageRequest_WithCustomerName_FullParse()
    {
        // Arrange
        const string path = "/iiif-img/test-customer/1/the-astronaut/full/!800,400/0/default.jpg";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("test-customer"))
            .Returns(customer);

        // Act
        var imageRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be("test-customer");
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be("/iiif-img/test-customer/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/!800,400/0/default.jpg");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/!800,400/0/default.jpg");
    }

    [Fact]
    public async Task Parse_ImageRequest_HandlesEscapedUrl()
    {
        // Arrange
        const string path = "/iiif-img/test-customer/1/the-astronaut/full/%5E!800,400/0/default.jpg";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("test-customer"))
            .Returns(customer);

        // Act
        var imageRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be("test-customer");
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be("/iiif-img/test-customer/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/^!800,400/0/default.jpg");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/^!800,400/0/default.jpg");
    }
    
    [Fact]
    public async Task Parse_ImageRequest_WithCustomerNameStartingV_IsNotParsedAsVersioned()
    {
        // Arrange
        const string path = "/iiif-img/v33/1/the-astronaut/full/!800,400/0/default.jpg";
        var customer = new CustomerPathElement(99, "v33");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("v33"))
            .Returns(customer);

        // Act
        var imageRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be("v33");
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be("/iiif-img/v33/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/!800,400/0/default.jpg");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/!800,400/0/default.jpg");
    }
    
    [Theory]
    [InlineData("test-customer")]
    [InlineData("99")]
    public async Task Parse_ImageRequest_SetsNormalisedPath(string customerPathValue)
    {
        // Arrange
        var path = $"/iiif-img/{customerPathValue}/1/the-astronaut/full/!800,400/0/default.jpg";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement(A<string>._)).Returns(customer);

        // Act
        var imageRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be(customerPathValue);
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be($"/iiif-img/{customerPathValue}/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/!800,400/0/default.jpg");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/!800,400/0/default.jpg");
        imageRequest.NormalisedBasePath.Should().Be("/iiif-img/99/1/");
        imageRequest.NormalisedFullPath.Should().Be("/iiif-img/99/1/the-astronaut/full/!800,400/0/default.jpg");
    }
    
    [Theory]
    [InlineData("v2", "test-customer")]
    [InlineData("v3", "99")]
    public async Task Parse_VersionedImageRequest_SetsNormalisedPath(string version, string customerPathValue)
    {
        // Arrange
        var path = $"/iiif-img/{version}/{customerPathValue}/1/the-astronaut/full/!800,400/0/default.jpg";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement(A<string>._)).Returns(customer);

        // Act
        var imageRequest = await sut.Parse<ImageAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-img");
        imageRequest.VersionedRoutePrefix.Should().Be($"iiif-img/{version}");
        imageRequest.VersionPathValue.Should().Be($"{version}");
        imageRequest.CustomerPathValue.Should().Be(customerPathValue);
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be($"/iiif-img/{version}/{customerPathValue}/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/!800,400/0/default.jpg");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/!800,400/0/default.jpg");
        imageRequest.NormalisedBasePath.Should().Be($"/iiif-img/{version}/99/1/");
        imageRequest.NormalisedFullPath.Should()
            .Be($"/iiif-img/{version}/99/1/the-astronaut/full/!800,400/0/default.jpg");
    }
    
    [Theory]
    [InlineData("/iiif-img/full/!800,400/0/default.jpg")]
    [InlineData("/iiif-av/test-customer/1/the-astronaut/full/full/max/max/0/default.mp3")]
    public async Task Parse_ImageRequest_ThrowsFormatException_IfPathInUnknownFormat(string path)
    {
        // Act
        Func<Task> action = () => sut.Parse<ImageAssetDeliveryRequest>(path);
        
        // Assert
        await action.Should().ThrowAsync<FormatException>();
    }
    
    [Fact]
    public async Task Parse_TimeBasedRequest_WithCustomerName_FullParse()
    {
        // Arrange
        const string path = "/iiif-av/test-customer/1/the-astronaut/full/full/max/max/0/default.mp4";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("test-customer"))
            .Returns(customer);

        // Act
        var imageRequest = await sut.Parse<TimeBasedAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-av");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-av");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be("test-customer");
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be("/iiif-av/test-customer/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/full/max/max/0/default.mp4");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.TimeBasedRequest.Should().Be("/full/full/max/max/0/default.mp4");
    }
    
    [Theory]
    [InlineData("test-customer")]
    [InlineData("99")]
    public async Task Parse_TimeBasedRequest_SetsNormalisedPath(string customerPathValue)
    {
        // Arrange
        var path = $"/iiif-av/{customerPathValue}/1/the-astronaut/full/full/max/max/0/default.mp4";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement(A<string>._)).Returns(customer);

        // Act
        var imageRequest = await sut.Parse<TimeBasedAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("iiif-av");
        imageRequest.VersionedRoutePrefix.Should().Be("iiif-av");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be(customerPathValue);
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be($"/iiif-av/{customerPathValue}/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut/full/full/max/max/0/default.mp4");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.TimeBasedRequest.Should().Be("/full/full/max/max/0/default.mp4");
        imageRequest.NormalisedBasePath.Should().Be("/iiif-av/99/1/");
        imageRequest.NormalisedFullPath.Should().Be("/iiif-av/99/1/the-astronaut/full/full/max/max/0/default.mp4");
    }

    [Fact]
    public async Task Parse_FileRequest_Parse()
    {
        // Arrange
        const string path = "/file/test-customer/1/the-astronaut";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement("test-customer"))
            .Returns(customer);

        // Act
        var imageRequest = await sut.Parse<FileAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("file");
        imageRequest.VersionedRoutePrefix.Should().Be("file");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be("test-customer");
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be("/file/test-customer/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut");
        imageRequest.AssetId.Should().Be("the-astronaut");
    }
    
    [Theory]
    [InlineData("test-customer")]
    [InlineData("99")]
    public async Task Parse_FileRequest_SetsNormalisedPath(string customerPathValue)
    {
        // Arrange
        var path = $"/file/{customerPathValue}/1/the-astronaut";
        var customer = new CustomerPathElement(99, "test-customer");
        A.CallTo(() => pathCustomerRepository.GetCustomerPathElement(A<string>._)).Returns(customer);

        // Act
        var imageRequest = await sut.Parse<FileAssetDeliveryRequest>(path);

        // Assert
        imageRequest.RoutePrefix.Should().Be("file");
        imageRequest.VersionedRoutePrefix.Should().Be("file");
        imageRequest.VersionPathValue.Should().BeNull();
        imageRequest.CustomerPathValue.Should().Be(customerPathValue);
        imageRequest.Customer.Should().Be(customer);
        imageRequest.BasePath.Should().Be($"/file/{customerPathValue}/1/");
        imageRequest.Space.Should().Be(1);
        imageRequest.AssetPath.Should().Be("the-astronaut");
        imageRequest.AssetId.Should().Be("the-astronaut");
        imageRequest.NormalisedBasePath.Should().Be("/file/99/1/");
        imageRequest.NormalisedFullPath.Should().Be("/file/99/1/the-astronaut");
    }
}