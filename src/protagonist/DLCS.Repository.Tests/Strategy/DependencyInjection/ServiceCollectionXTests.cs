using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using DLCS.Repository.OriginStrategies;
using DLCS.Repository.Strategy;
using DLCS.Repository.Strategy.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DLCS.Repository.Tests.Strategy.DependencyInjection;

public class ServiceCollectionXTests
{
    [Theory]
    [InlineData("http://127.0.0.1/foo")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.1.2.3/foo")]
    public async Task AddOriginStrategies_ConfiguresHttpClient_ToBlockOrigin(string origin)
    {
        // Arrange
        var httpClient = GetOriginStrategyHttpClient(new Dictionary<string, string?>
        {
            ["OriginStrategy:BlockedIpRanges:0"] = "10.0.0.0/8"
        });

        // Act
        Func<Task> action = () => httpClient.GetAsync(origin);

        // Assert
        var exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.InnerException.Should().BeOfType<OriginAddressBlockedException>();
    }

    [Fact]
    public async Task AddOriginStrategies_ConfiguresHttpClient_ToAllowOrigin_IfInAllowedRange()
    {
        // Arrange
        var httpClient = GetOriginStrategyHttpClient(new Dictionary<string, string?>
        {
            ["OriginStrategy:AllowedIpRanges:0"] = "127.0.0.0/8"
        });

        // Act - port 1 has nothing listening, so we get as far as a refused connection
        Func<Task> action = () => httpClient.GetAsync("http://127.0.0.1:1/foo");

        // Assert
        var exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.InnerException.Should().BeOfType<SocketException>("the guard should not have blocked the address");
    }

    [Fact]
    public async Task AddOriginStrategies_ConfiguresHttpClient_ToBlockInstanceMetadata_EvenIfAllowed()
    {
        // Arrange
        var httpClient = GetOriginStrategyHttpClient(new Dictionary<string, string?>
        {
            ["OriginStrategy:AllowedIpRanges:0"] = "0.0.0.0/0"
        });

        // Act
        Func<Task> action = () => httpClient.GetAsync("http://169.254.169.254/latest/meta-data");

        // Assert
        var exception = (await action.Should().ThrowAsync<HttpRequestException>()).Which;
        exception.InnerException.Should().BeOfType<OriginAddressBlockedException>();
    }

    [Theory]
    [InlineData("BlockedIpRanges")]
    [InlineData("AllowedIpRanges")]
    public void AddOriginStrategies_Throws_IfIpRangeInvalid(string setting)
    {
        // Arrange
        var configuration = GetConfiguration(new Dictionary<string, string?>
        {
            [$"OriginStrategy:{setting}:0"] = "not-a-range"
        });

        // Act
        Action action = () => new ServiceCollection().AddOriginStrategies(configuration);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"*{setting}*not-a-range*");
    }

    private static HttpClient GetOriginStrategyHttpClient(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddOriginStrategies(GetConfiguration(settings))
            .BuildServiceProvider();

        var httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClients.OriginStrategy);
        httpClient.Timeout = TimeSpan.FromSeconds(5);
        return httpClient;
    }

    private static IConfiguration GetConfiguration(Dictionary<string, string?> settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
