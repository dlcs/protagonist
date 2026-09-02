using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customers;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.Network;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Http;

namespace DLCS.Repository.Tests.Strategy;

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
        var result = await sut.LoadFromOrigin(new Asset { Id = assetId, Origin = originUri }, new CustomerOriginStrategy());

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
        var result = await sut.LoadFromOrigin(new Asset { Id = assetId, Origin = originUri }, new CustomerOriginStrategy());

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
        var result = await sut.LoadFromOrigin(new Asset { Id = assetId, Origin = originUri }, new CustomerOriginStrategy());

        // Assert
        httpHandler.CallsMade.Should().Contain(originUri);
        result.Stream.Should().BeSameAs(Stream.Null);
        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAssetFromOrigin_Throws_IfOriginBlocked()
    {
        // Arrange
        httpHandler.RegisterCallback(_ => throw GetBlockedException());
        const string originUri = "https://test.example.com/string";

        // Act
        Func<Task> action = () =>
            sut.LoadFromOrigin(new Asset { Id = assetId, Origin = originUri }, new CustomerOriginStrategy());

        // Assert
        await action.Should().ThrowAsync<OriginAddressBlockedException>();
    }

    private static HttpRequestException GetBlockedException()
        => new("blocked",
            new OriginAddressBlockedException("test.example.com", IPAddress.Loopback, IPNetwork.Parse("127.0.0.0/8")));
}
