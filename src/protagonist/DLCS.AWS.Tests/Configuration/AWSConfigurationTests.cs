using Amazon.MediaConvert;
using Amazon.S3;
using Amazon.SQS;
using DLCS.AWS.Configuration;
using FakeItEasy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DLCS.AWS.Tests.Configuration;

public class AWSConfigurationTests
{
    [Fact]
    public void WithCustomerScopedAmazonS3_RegistersCustomerScopedProvider_IfAssumeRoleEnabled()
    {
        // Arrange
        var services = GetServiceCollection();

        // Act
        services.SetupAWS(GetConfiguration(assumeRoleEnabled: true), GetEnvironment())
            .WithCustomerScopedAmazonS3();
        var serviceProvider = BuildServiceProvider(services);

        // Assert
        serviceProvider.GetRequiredService<IAwsClientProvider<IAmazonS3>>()
            .Should().BeOfType<CustomerScopedAwsClientProvider<IAmazonS3>>();
    }

    [Fact]
    public void WithCustomerScopedAmazonS3_RegistersAmbientProvider_IfAssumeRoleDisabled()
    {
        // Arrange
        var services = GetServiceCollection();

        // Act
        services.SetupAWS(GetConfiguration(assumeRoleEnabled: false), GetEnvironment())
            .WithCustomerScopedAmazonS3();
        var serviceProvider = BuildServiceProvider(services);

        // Assert
        serviceProvider.GetRequiredService<IAwsClientProvider<IAmazonS3>>()
            .Should().BeOfType<AmbientAwsClientProvider<IAmazonS3>>();
    }

    [Fact]
    public void WithCustomerScopedAmazonS3_RegistersAmbientProvider_IfUsingLocalStack()
    {
        // LocalStack doesn't support the STS operations required to assume a customer-scoped role
        // Arrange
        var services = GetServiceCollection();
        var configuration = GetConfiguration(assumeRoleEnabled: true, useLocalStack: true);

        // Act
        services.SetupAWS(configuration, GetEnvironment(Environments.Development))
            .WithCustomerScopedAmazonS3();
        var serviceProvider = BuildServiceProvider(services);

        // Assert
        serviceProvider.GetRequiredService<IAwsClientProvider<IAmazonS3>>()
            .Should().BeOfType<AmbientAwsClientProvider<IAmazonS3>>();
    }

    [Fact]
    public void WithCustomerScopedAmazonS3_Throws_IfAssumeRoleEnabledWithoutRoleArn()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = GetConfiguration(assumeRoleEnabled: true, roleArn: null);

        // Act
        Action action = () => services.SetupAWS(configuration, GetEnvironment()).WithCustomerScopedAmazonS3();

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CustomerScopedClients_DoNotAffectOtherClientTypes()
    {
        // SQS is not customer scoped - the queue listener polls before any customer is known
        // Arrange
        var services = GetServiceCollection();

        // Act
        services.SetupAWS(GetConfiguration(assumeRoleEnabled: true), GetEnvironment())
            .WithCustomerScopedAmazonS3()
            .WithAmazonSQS()
            .WithCustomerScopedMediaConvert();
        var serviceProvider = BuildServiceProvider(services);

        // Assert
        serviceProvider.GetRequiredService<IAwsClientProvider<IAmazonSQS>>()
            .Should().BeOfType<AmbientAwsClientProvider<IAmazonSQS>>();
        serviceProvider.GetRequiredService<IAwsClientProvider<IAmazonMediaConvert>>()
            .Should().BeOfType<CustomerScopedAwsClientProvider<IAmazonMediaConvert>>();
    }

    private static IServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        return services;
    }

    /// <summary>
    /// Build ServiceProvider, replacing AWS clients with fakes. The ambient provider resolves the client itself, which
    /// would otherwise require resolvable AWS credentials
    /// </summary>
    private static ServiceProvider BuildServiceProvider(IServiceCollection services)
    {
        services.AddSingleton(A.Fake<IAmazonS3>());
        services.AddSingleton(A.Fake<IAmazonSQS>());
        services.AddSingleton(A.Fake<IAmazonMediaConvert>());
        return services.BuildServiceProvider();
    }

    private static IConfiguration GetConfiguration(bool assumeRoleEnabled, bool useLocalStack = false,
        string? roleArn = "arn:aws:iam::123456789012:role/EngineRole")
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:Region"] = "eu-west-1",
                ["AWS:UseLocalStack"] = useLocalStack.ToString(),
                ["AWS:S3:ServiceUrl"] = "http://localhost:4566",
                ["AWS:SQS:ServiceUrl"] = "http://localhost:4566",
                ["AWS:AssumeRole:Enabled"] = assumeRoleEnabled.ToString(),
                ["AWS:AssumeRole:RoleArn"] = roleArn
            })
            .Build();

    private static IHostEnvironment GetEnvironment(string? environmentName = null)
    {
        var hostEnvironment = A.Fake<IHostEnvironment>();
        A.CallTo(() => hostEnvironment.EnvironmentName).Returns(environmentName ?? Environments.Production);
        return hostEnvironment;
    }
}
