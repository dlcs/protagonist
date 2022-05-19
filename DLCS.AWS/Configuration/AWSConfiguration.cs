using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DLCS.AWS.Configuration
{
    /// <summary>
    /// Base class for wiring up AWS dependencies. Handles using either LocalStack or AWS dependant on config. 
    /// </summary>
    public static class AWSConfiguration
    {
        /// <summary>
        /// Setup AWS environment by configuring appropriate services.
        /// </summary>
        public static AwsBuilder SetupAWS(this IServiceCollection services, IConfiguration configuration,
            IWebHostEnvironment environment)
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
        /// <param name="lifetime">ServiceLifetime for dependency, </param>
        /// <returns></returns>
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
                /*services.AddSingleton<IAmazonS3>(_ =>
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
                });*/
            }
            else
            {
                services.AddAWSService<IAmazonS3>(lifetime);
            }
            
            return this;
        }
    }
}