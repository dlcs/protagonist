﻿using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using DLCS.AWS.Settings;
using DLCS.Core.Guard;
using Microsoft.AspNetCore.Hosting;
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
                var amazonS3Config = new AmazonSQSConfig
                {
                    UseHttp = true,
                    RegionEndpoint = RegionEndpoint.USEast1,
                    ServiceURL =
                        awsSettings.SQS.ServiceUrl.ThrowIfNullOrWhiteSpace(nameof(awsSettings.SQS.ServiceUrl)),
                };
                return new AmazonSQSClient(new BasicAWSCredentials("foo", "bar"), amazonS3Config);
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
    /// Add <see cref="Amazon.Runtime.IAmazonService"/> to service collection with specified lifetime.
    /// If using localStack this will be a no-op.
    /// </summary>
    /// <param name="lifetime">ServiceLifetime for dependency</param>
    /// <typeparam name="T">Type of amazon service service to add</typeparam>
    /// <returns></returns>
    public AwsBuilder WithAWSService<T>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where T : IAmazonService
    {
        if (!useLocalStack)
        {
            services.AddAWSService<T>(lifetime);
        }
        
        return this;
    }
}