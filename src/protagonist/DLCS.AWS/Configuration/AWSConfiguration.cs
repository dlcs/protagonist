using Amazon;
using Amazon.CloudFront;
using Amazon.MediaConvert;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        return new AwsBuilder(awsSettings, services, useLocalStack);
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

    public AwsBuilder WithAmazonCloudfront(ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        // TODO - if using localStack this should register a dummy client or we may make requests to CF
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
