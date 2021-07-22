using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
using FluentAssertions;
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
        private readonly ImageRequestHandler sut;
        
        public ImageRequestHandlerTests()
        {
            assetRepository = A.Fake<IAssetRepository>();
            thumbnailRepository = A.Fake<IThumbRepository>();
            assetDeliveryPathParser = A.Fake<IAssetDeliveryPathParser>();

            sut = new ImageRequestHandler(new NullLogger<ImageRequestHandler>(), assetRepository, thumbnailRepository,
                assetDeliveryPathParser);
        }

        [Fact]
        public async Task HandleRequest_Returns404_IfAssetPathParserThrowsKeyNotFound()
        {
            // Arrange
            A.CallTo(() => assetDeliveryPathParser.Parse(A<string>._)).ThrowsAsync(new KeyNotFoundException());
            
            // Act
            var result = await sut.HandleRequest(new DefaultHttpContext());
            
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
            var result = await sut.HandleRequest(new DefaultHttpContext());
            
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
            var result = await sut.HandleRequest(new DefaultHttpContext());
            
            // Assert
            result.Should().BeOfType<StatusCodeProxyResult>()
                .Which.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
    }
}