using Amazon;
using Amazon.CloudFront;
using Amazon.Extensions.NETCore.Setup;
using Amazon.MediaConvert;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.AWS.Configuration;

/// <summary>
/// Base class for wiring up AWS dependencies. Handles using either LocalStack or AWS dependant on config. 
/// </summary>
public static class AWSConfiguration
{
    /// <summary>
    /// Setup AWS environment by configuring appropriate services.
    /// </summary>
    public static AwsBuilder SetupAWS(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        IConfigurationSection? configurationSection = configuration.GetSection("AWS");
        services.Configure<AWSSettings>(configurationSection);
        var awsSettings = configurationSection.Get<AWSSettings>();

        var useLocalStack = environment.IsDevelopment() && awsSettings.UseLocalStack;

        if (!useLocalStack)
        {
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        }

        services.AddAmbientAwsClientProviders();

        return new AwsBuilder(awsSettings, services, useLocalStack);
    }

    /// <summary>
    /// Register the default, ambient, <see cref="IAwsClientProvider{T}"/> implementation - this provides clients that
    /// use the ambient credentials for the current process, for every customer. Services that require customer-scoped
    /// clients opt in via the WithCustomerScoped* methods on <see cref="AwsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// This is called by <see cref="SetupAWS"/>. Services that register AWS clients without using it must call this
    /// directly, else consumers of <see cref="IAwsClientProvider{T}"/> will fail to resolve.
    /// </remarks>
    public static IServiceCollection AddAmbientAwsClientProviders(this IServiceCollection services)
    {
        services.TryAdd(ServiceDescriptor.Singleton(typeof(IAwsClientProvider<>), typeof(AmbientAwsClientProvider<>)));
        services.TryAddSingleton<ICustomerAwsContext, AsyncLocalCustomerAwsContext>();
        return services;
    }
}

/// <summary>
/// Wrapper around awssdk.extensions.netcore.setup methods for configuring AWS services.
/// Switches between 'real' AWS and LocalStack depending on configuration settings.
/// If "AWS:UseLocalStack" = true, and environment = Develop then localstack used. Else AWS
/// </summary>
public class AwsBuilder
{
    private readonly AWSSettings awsSettings;
    private readonly IServiceCollection services;
    private readonly bool useLocalStack;

    public AwsBuilder(
        AWSSettings awsSettings,
        IServiceCollection services,
        bool useLocalStack)
    {
        this.awsSettings = awsSettings;
        this.services = services;
        this.useLocalStack = useLocalStack;
    }

    /// <summary>
    /// Add <see cref="IAmazonS3"/> to service collection with specified lifetime.
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns>Current <see cref="AwsBuilder"/> instance</returns>
    public AwsBuilder WithAmazonS3(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (useLocalStack)
        {
            var serviceDescriptor = ServiceDescriptor.Describe(typeof(IAmazonS3), _ =>
            {
                var amazonS3Config = new AmazonS3Config
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL =
                        awsSettings.S3?.ServiceUrl.ThrowIfNullOrWhiteSpace(nameof(awsSettings.S3.ServiceUrl)),
                    ForcePathStyle = true
                };
                return new AmazonS3Client(new BasicAWSCredentials("foo", "bar"), amazonS3Config);
            }, lifetime);
            services.Add(serviceDescriptor);
        }
        else
        {
            services.AddAWSService<IAmazonS3>(lifetime);
        }
        
        return this;
    }
    
    /// <summary>
    /// Add <see cref="IAmazonSQS"/> to service collection with specified lifetime.
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns></returns>
    public AwsBuilder WithAmazonSQS(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (useLocalStack)
        {
            var serviceDescriptor = ServiceDescriptor.Describe(typeof(IAmazonSQS), _ =>
            {
                var amazonSQSConfig = new AmazonSQSConfig
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL =
                        awsSettings.SQS.ServiceUrl.ThrowIfNullOrWhiteSpace(nameof(awsSettings.SQS.ServiceUrl)),
                };
                return new AmazonSQSClient(new BasicAWSCredentials("foo", "bar"), amazonSQSConfig);
            }, lifetime);
            services.Add(serviceDescriptor);
        }
        else
        {
            services.AddAWSService<IAmazonSQS>(lifetime);
        }
        
        return this;
    }
    
    /// <summary>
    /// Add <see cref="IAmazonSimpleNotificationService"/> to service collection with specified lifetime.
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns></returns>
    public AwsBuilder WithAmazonSNS(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (useLocalStack)
        {
            var serviceDescriptor = ServiceDescriptor.Describe(typeof(IAmazonSimpleNotificationService), _ =>
            {
                var amazonSNSConfig = new AmazonSimpleNotificationServiceConfig()
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL =
                        awsSettings.SNS.ServiceUrl.ThrowIfNullOrWhiteSpace(nameof(awsSettings.SNS.ServiceUrl)),
                };
                return new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials("foo", "bar"), amazonSNSConfig);
            }, lifetime);
            services.Add(serviceDescriptor);
        }
        else
        {
            services.AddAWSService<IAmazonSimpleNotificationService>(lifetime);
        }
        
        return this;
    }

    /// <summary>
    /// Add <see cref="IAmazonS3"/> to service collection, using a customer-scoped client if "AWS:AssumeRole" is
    /// enabled. See <see cref="WithCustomerScopedClient{T}"/>
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns>Current <see cref="AwsBuilder"/> instance</returns>
    public AwsBuilder WithCustomerScopedAmazonS3(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        WithAmazonS3(lifetime);
        return WithCustomerScopedClient<IAmazonS3>();
    }

    /// <summary>
    /// Add <see cref="IAmazonSimpleNotificationService"/> to service collection, using a customer-scoped client if
    /// "AWS:AssumeRole" is enabled. See <see cref="WithCustomerScopedClient{T}"/>
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns>Current <see cref="AwsBuilder"/> instance</returns>
    public AwsBuilder WithCustomerScopedAmazonSNS(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        WithAmazonSNS(lifetime);
        return WithCustomerScopedClient<IAmazonSimpleNotificationService>();
    }

    /// <summary>
    /// Add <see cref="IAmazonMediaConvert"/> to service collection, using a customer-scoped client if
    /// "AWS:AssumeRole" is enabled. See <see cref="WithCustomerScopedClient{T}"/>
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <returns>Current <see cref="AwsBuilder"/> instance</returns>
    public AwsBuilder WithCustomerScopedMediaConvert(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        WithMediaConvert(lifetime);
        return WithCustomerScopedClient<IAmazonMediaConvert>();
    }

    /// <summary>
    /// Register <see cref="IAwsClientProvider{T}"/> that provides clients scoped to the customer currently being
    /// processed. Consumers take a dependency on <see cref="IAwsClientProvider{T}"/> rather than the client itself,
    /// so are unaware of which is in use.
    /// </summary>
    /// <remarks>
    /// This is a no-op unless "AWS:AssumeRole:Enabled" is true, LocalStack does not support the required STS
    /// operations so this is also skipped if LocalStack is in use.
    /// </remarks>
    private AwsBuilder WithCustomerScopedClient<T>() where T : class, IAmazonService
    {
        var assumeRoleSettings = awsSettings.AssumeRole;
        if (!assumeRoleSettings.Enabled || useLocalStack) return this;

        assumeRoleSettings.RoleArn.ThrowIfNullOrWhiteSpace(
            $"{nameof(AWSSettings.AssumeRole)}:{nameof(AssumeRoleSettings.RoleArn)}");

        services.TryAddSingleton<ICustomerAwsCredentials, AssumedRoleCustomerAwsCredentials>();

        // NOTE: this closed generic registration takes precedence over the open generic ambient provider
        services.AddSingleton<IAwsClientProvider<T>>(provider => new CustomerScopedAwsClientProvider<T>(
            provider.GetRequiredService<ICustomerAwsContext>(),
            provider.GetRequiredService<ICustomerAwsCredentials>(),
            provider.GetRequiredService<IOptions<AWSSettings>>(),
            provider.GetRequiredService<ILogger<CustomerScopedAwsClientProvider<T>>>(),
            provider.GetService<AWSOptions>()));

        return this;
    }

    public AwsBuilder WithAmazonCloudfront(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        services.AddAWSService<IAmazonCloudFront>(lifetime);
        
        return this;
    }
    
    public AwsBuilder WithMediaConvert(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        if (useLocalStack)
        {
            // LocalStack MediaConvert support is in preview and requires PRO image. Register a dummy client to allow
            // service to run without risk of accessing 'real' client. Use S3.ServiceUrl as this is for localstack
            // and serves as a placeholder
            var serviceDescriptor = ServiceDescriptor.Describe(typeof(IAmazonMediaConvert), _ =>
            {
                var mediaConvertConfig = new AmazonMediaConvertConfig
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL =
                        awsSettings.S3.ServiceUrl.ThrowIfNullOrWhiteSpace(nameof(awsSettings.S3.ServiceUrl)),
                };
                return new AmazonMediaConvertClient(new BasicAWSCredentials("foo", "bar"), mediaConvertConfig);
            }, lifetime);
            services.Add(serviceDescriptor);
        }
        else
        {
            services.AddAWSService<IAmazonMediaConvert>(lifetime);
        }

        return this;
    }
}
