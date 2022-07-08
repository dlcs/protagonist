using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Http;
using Xunit;

namespace DLCS.Repository.Tests.Strategy
{
    public class DefaultOriginStrategyTests
    {
        private readonly DefaultOriginStrategy sut;
        private readonly ControllableHttpMessageHandler httpHandler;
        private readonly AssetId assetId = new(2, 2, "foo");

        public DefaultOriginStrategyTests()
        {
            httpHandler = new ControllableHttpMessageHandler();

            var httpClientFactory = A.Fake<IHttpClientFactory>();
            var httpClient = new HttpClient(httpHandler);
            A.CallTo(() => httpClientFactory.CreateClient("OriginStrategy")).Returns(httpClient);
            
            sut = new DefaultOriginStrategy(httpClientFactory, new NullLogger<DefaultOriginStrategy>());
        }

        [Fact]
        public async Task LoadAssetFromOrigin_ReturnsExpectedResponse_OnSuccess()
        {
            // Arrange
            var response = httpHandler.GetResponseMessage("this is a test", HttpStatusCode.OK);
            const string contentType = "application/json";
            const long contentLength = 4324;
            
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            response.Content.Headers.ContentLength = contentLength;
            httpHandler.SetResponse(response);

            const string originUri = "https://test.example.com/string";
            
            // Act
            var result = await sut.LoadAssetFromOrigin(assetId, originUri, new CustomerOriginStrategy());
            
            // Assert
            httpHandler.CallsMade.Should().Contain(originUri);
            result.Stream.Should().NotBeNull();
            result.ContentLength.Should().Be(contentLength);
            result.ContentType.Should().Be(contentType);
        }
        
        [Fact]
        public async Task LoadAssetFromOrigin_HandlesNoContentLengthAndType()
        {
            // Arrange
            var response = httpHandler.GetResponseMessage("", HttpStatusCode.OK);
            httpHandler.SetResponse(response);
            const string originUri = "https://test.example.com/string";
            
            // Act
            var result = await sut.LoadAssetFromOrigin(assetId, originUri, new CustomerOriginStrategy());
            
            // Assert
            httpHandler.CallsMade.Should().Contain(originUri);
            result.Stream.Should().NotBeNull();
            result.ContentLength.Should().BeNull();
            result.ContentType.Should().Be("text/plain");
        }
        
        [Theory]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task LoadAssetFromOrigin_ReturnsNull_IfCallFails(HttpStatusCode statusCode)
        {
            // Arrange
            var response = httpHandler.GetResponseMessage("uh-oh", statusCode);
            httpHandler.SetResponse(response);
            const string originUri = "https://test.example.com/string";
            
            // Act
            var result = await sut.LoadAssetFromOrigin(assetId, originUri, new CustomerOriginStrategy());
            
            // Assert
            httpHandler.CallsMade.Should().Contain(originUri);
            result.Stream.Should().BeSameAs(Stream.Null);
            result.IsEmpty.Should().BeTrue();
        }
    }
}