using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
using FluentAssertions;
using IIIF.ImageApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Images;
using Orchestrator.ReverseProxy;
using Xunit;

namespace Orchestrator.Tests.Images
{
    public class ImageRequestHandlerTests
    {
        private readonly IAssetRepository assetRepository;
        private readonly IThumbRepository thumbnailRepository;
        private readonly IAssetDeliveryPathParser assetDeliveryPathParser;
        private readonly IPathCustomerRepository customerRepository;
        private readonly ImageRequestHandler sut;
        
        public ImageRequestHandlerTests()
        {
            assetRepository = A.Fake<IAssetRepository>();
            thumbnailRepository = A.Fake<IThumbRepository>();
            assetDeliveryPathParser = A.Fake<IAssetDeliveryPathParser>();
            customerRepository = A.Fake<IPathCustomerRepository>();
            var assetDeliveryPathParserImpl = new AssetDeliveryPathParser(customerRepository);

            sut = new ImageRequestHandler(new NullLogger<ImageRequestHandler>(), assetRepository, thumbnailRepository,
                assetDeliveryPathParserImpl);
        }

        [Fact]
        public async Task HandleRequest_Returns404_IfAssetPathParserThrowsKeyNotFound()
        {
            // Arrange
            A.CallTo(() => assetDeliveryPathParser.Parse(A<string>._)).ThrowsAsync(new KeyNotFoundException());
            
            // Act
            var systemUnderTest = GetImageRequestHandlerWithMockePathParser();
            var result = await systemUnderTest.HandleRequest(new DefaultHttpContext());
            
            // Assert
            result.Should().BeOfType<StatusCodeProxyResult>().Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task HandleRequest_Returns400_IfAssetPathParserThrowsFormatException()
        {
            // NOTE - routes should prevent this from ever happening
            
            // Arrange
            A.CallTo(() => assetDeliveryPathParser.Parse(A<string>._)).ThrowsAsync(new FormatException());
            
            // Act
            var systemUnderTest = GetImageRequestHandlerWithMockePathParser();
            var result = await systemUnderTest.HandleRequest(new DefaultHttpContext());
            
            // Assert
            result.Should().BeOfType<StatusCodeProxyResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        
        [Fact]
        public async Task HandleRequest_Returns400_IfAssetPathParserThrowsException()
        {
            // NOTE - routes should prevent this from ever happening
            
            // Arrange
            A.CallTo(() => assetDeliveryPathParser.Parse(A<string>._)).ThrowsAsync(new ApplicationException());
            
            // Act
            var systemUnderTest = GetImageRequestHandlerWithMockePathParser();
            var result = await systemUnderTest.HandleRequest(new DefaultHttpContext());
            
            // Assert
            result.Should().BeOfType<StatusCodeProxyResult>()
                .Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Handle_Request_ProxiesToOrchestrator_IfAssetRequiresAuth()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/!200,200/0/default.jpg";

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetRepository.GetAsset("2/2/test-image")).Returns(new Asset {Roles = "admin"});

            // Act
            var result = await sut.HandleRequest(context) as ProxyActionResult;
            
            // Assert
            result.Target.Should().Be(ProxyTo.Orchestrator);
            result.HasPath.Should().BeFalse();
        }
        
        [Fact]
        public async Task Handle_Request_ProxiesToThumbs_OnNewPath_IfAssetIsForUvThumb()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/90,/0/default.jpg";
            context.Request.QueryString = new QueryString("?t=123123");

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetRepository.GetAsset("2/2/test-image")).Returns(new Asset());

            // Act
            var result = await sut.HandleRequest(context) as ProxyActionResult;
            
            // Assert
            result.Target.Should().Be(ProxyTo.Thumbs);
            result.HasPath.Should().BeTrue();
            result.Path.Should().Be("thumbs/2/2/test-image/full/!200,200/0/default.jpg");
        }
        
        [Fact]
        public async Task Handle_Request_ProxiesToThumbs_IfFullOrMaxRegion_AndKnownSize()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/!100,150/0/default.jpg";

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetRepository.GetAsset("2/2/test-image")).Returns(new Asset());
            A.CallTo(() => thumbnailRepository.GetThumbnailSizeCandidate(2, 2, A<ImageRequest>._))
                .Returns(new SizeCandidate(150));

            // Act
            var result = await sut.HandleRequest(context) as ProxyActionResult;
            
            // Assert
            result.Target.Should().Be(ProxyTo.Thumbs);
            result.Path.Should().Be("thumbs/2/2/test-image/full/!100,150/0/default.jpg");
        }
        
        [Theory]
        [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg", false)] // UV without ?t=
        [InlineData("/iiif-img/2/2/test-image/full/full/0/default.jpg", true)] // /full/full
        [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg", true)] // /full/max
        public async Task Handle_Request_ProxiesToCachingProxy_ForAllOtherCases(string path, bool knownThumb)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetRepository.GetAsset("2/2/test-image")).Returns(new Asset());

            if (knownThumb)
            {
                A.CallTo(() => thumbnailRepository.GetThumbnailSizeCandidate(2, 2, A<ImageRequest>._))
                    .Returns(new SizeCandidate(150));
            }

            // Act
            var result = await sut.HandleRequest(context) as ProxyActionResult;
            
            // Assert
            result.Target.Should().Be(ProxyTo.CachingProxy);
            result.HasPath.Should().BeFalse();
        }

        private ImageRequestHandler GetImageRequestHandlerWithMockePathParser()
            => new(new NullLogger<ImageRequestHandler>(), assetRepository, thumbnailRepository,
                assetDeliveryPathParser);
    }
}