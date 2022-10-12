using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Encryption;
using DLCS.Core.Types;
using DLCS.Model.Customers;
using DLCS.Web.Auth;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.Deliverator;
using Orchestrator.Settings;
using Test.Helpers.Http;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.Deliverator;

public class DeliveratorApiClientTests
{
    private readonly ControllableHttpMessageHandler httpHandler;
    private readonly ICustomerRepository customerRepository;
    private readonly DeliveratorApiClient sut;

    public DeliveratorApiClientTests()
    {
        httpHandler = new ControllableHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        httpClient.BaseAddress = new Uri("https://test.api");
        var options = Options.Create(new OrchestratorSettings { ApiSalt = "foobar" });

        var encryption = A.Fake<IEncryption>();
        A.CallTo(() => encryption.Encrypt(A<string>._)).Returns("encrypted");
        customerRepository = A.Fake<ICustomerRepository>();

        sut = new DeliveratorApiClient(httpClient, new DlcsApiAuth(encryption), customerRepository, options,
            new NullLogger<DeliveratorApiClient>());
    }
    
    [Fact]
    public async Task Reingest_ReturnsFalse_IfCustomerNotFound()
    {
        // Arrange
        var assetId = new AssetId(1, 10, "test-asset");
        
        // Act
        var response = await sut.ReingestAsset(assetId);
        
        // Assert
        response.Should().BeFalse();
    }

    [Fact]
    public async Task Reingest_PostsToCorrectUri_WithAuth()
    {
        // Arrange
        var assetId = new AssetId(1, 10, "test-asset");
        A.CallTo(() => customerRepository.GetCustomer(1)).Returns(new Customer { Keys = new[] { "key1" } });
        
        HttpRequestMessage message = null;
        httpHandler.RegisterCallback(r => message = r);
        
        // Act
        await sut.ReingestAsset(assetId);
        
        // Assert
        httpHandler.CallsMade.Should().ContainSingle()
            .Which.Should().Be("https://test.api/customers/1/spaces/10/images/test-asset/reingest");
        message.Method.Should().Be(HttpMethod.Post);
        message.Headers.Authorization.Scheme.Should().Be("Basic");
    }
    
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Reingest_ReturnsFalse_IfNon200StatusResponse(HttpStatusCode statusCode)
    {
        // Arrange
        var assetId = new AssetId(1, 10, "test-asset");
        A.CallTo(() => customerRepository.GetCustomer(1)).Returns(new Customer { Keys = new[] { "key1" } });
        
        httpHandler.SetResponse(new HttpResponseMessage(statusCode));
        
        // Act
        var response = await sut.ReingestAsset(assetId);
        
        // Assert
        response.Should().BeFalse();
    }
    
    [Fact]
    public async Task Reingest_ReturnsTrue_If200StatusResponse()
    {
        // Arrange
        var assetId = new AssetId(1, 10, "test-asset");
        A.CallTo(() => customerRepository.GetCustomer(1)).Returns(new Customer { Keys = new[] { "key1" } });
        
        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent("{\"error\":\"\"}", Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);
        
        // Act
        var response = await sut.ReingestAsset(assetId);
        
        // Assert
        response.Should().BeTrue();
    }
    
    [Fact]
    public async Task Reingest_ReturnsFalse_If200StatusResponseWithError()
    {
        // Arrange
        var assetId = new AssetId(1, 10, "test-asset");
        A.CallTo(() => customerRepository.GetCustomer(1)).Returns(new Customer { Keys = new[] { "key1" } });

        var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
        httpResponseMessage.Content = new StringContent("{\"error\":\"uhoh\"}", Encoding.UTF8, "application/json");
        httpHandler.SetResponse(httpResponseMessage);
        
        // Act
        var response = await sut.ReingestAsset(assetId);
        
        // Assert
        response.Should().BeFalse();
    }
}