﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Test.Helpers.Integration;

/// <summary>
/// Xunit fixture that manages localstack and contains faked AWS clients for interactions.
/// </summary>
public class LocalStackFixture : IAsyncLifetime
{
    private readonly IContainer localStackContainer;
    private const int LocalStackContainerPort = 4566;
    
    // S3 Buckets
    public const string OutputBucketName = "protagonist-output";
    public const string ThumbsBucketName = "protagonist-thumbs";
    public const string StorageBucketName = "protagonist-storage";
    public const string OriginBucketName = "protagonist-origin";
    public const string TimebasedInputBucketName = "protagonist-timebased-in";
    public const string TimebasedOutputBucketName = "protagonist-timebased-out";
    public const string SecurityObjectsBucketName = "protagonist-security-objects";
    
    // SQS Queues
    public const string ImageQueueName = "protagonist-image";
    public const string PriorityImageQueueName = "protagonist-priority-image";
    public const string TimebasedQueueName = "protagonist-timebased";
    public const string TranscodeCompleteQueueName = "protagonist-transcode-complete";
    public const string FileQueueName = "protagonist-file";
    
    // SNS Topic
    public const string AssetModifiedNotificationTopicArn = "arn:aws:sns:us-east-1:000000000000:asset-modified-notifications";
        
    public Func<IAmazonS3> AWSS3ClientFactory { get; private set; }
    public Func<IAmazonSQS> AWSSQSClientFactory { get; private set; }
    public Func<IAmazonSimpleNotificationService> AWSSNSFactory { get; private set; }
    

    public LocalStackFixture()
    {
        // Configure container binding to host port 0, which will use a random free port
        var localStackBuilder = new ContainerBuilder()
            .WithImage("localstack/localstack:4.3")
            .WithCleanUp(true)
            .WithLabel("protagonist_test", "True")
            .WithEnvironment("DEFAULT_REGION", "eu-west-1")
            .WithEnvironment("SERVICES", "s3,sqs,sns")
            .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
            .WithEnvironment("DEBUG", "1")
            .WithPortBinding(0, LocalStackContainerPort);

        localStackContainer = localStackBuilder.Build();
    }

    public async Task InitializeAsync()
    {
        // Start local stack + create any required resources
        await localStackContainer.StartAsync();
        SetAWSClientFactories();
        await SeedAwsResources();
    }

    public Task DisposeAsync() => localStackContainer.StopAsync();

    private void SetAWSClientFactories()
    {
        // Get the actual port number used as we bound to 0
        var localStackPort = localStackContainer.GetMappedPublicPort(LocalStackContainerPort);
        
        // LocalStack url
        var localStackUrl = $"http://localhost:{localStackPort}/";
        
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.EUWest1,
            UseHttp = true,
            ForcePathStyle = true,
            ServiceURL = localStackUrl
        };

        AWSS3ClientFactory = () => new AmazonS3Client(new BasicAWSCredentials("foo", "bar"), s3Config);
        
        var sqsConfig = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.EUWest1,
            UseHttp = true,
            ServiceURL = localStackUrl
        };

        AWSSQSClientFactory = () => new AmazonSQSClient(new BasicAWSCredentials("foo", "bar"), sqsConfig);

        var snsConfig = new AmazonSimpleNotificationServiceConfig
        {
            RegionEndpoint = RegionEndpoint.EUWest1,
            UseHttp = true,
            ServiceURL = localStackUrl
        };

        AWSSNSFactory = () => new AmazonSimpleNotificationServiceClient(new BasicAWSCredentials("foo", "bar"), snsConfig);
    }
    
    private async Task SeedAwsResources()
    {
        // Create basic buckets used by DLCS
        var amazonS3Client = AWSS3ClientFactory();
        await amazonS3Client.PutBucketAsync(OutputBucketName);
        await amazonS3Client.PutBucketAsync(ThumbsBucketName);
        await amazonS3Client.PutBucketAsync(StorageBucketName);
        await amazonS3Client.PutBucketAsync(TimebasedInputBucketName);
        await amazonS3Client.PutBucketAsync(TimebasedOutputBucketName);
        await amazonS3Client.PutBucketAsync(OriginBucketName);
        await amazonS3Client.PutBucketAsync(SecurityObjectsBucketName);

        // And SQS queues
        var amazonSQSClient = AWSSQSClientFactory();
        await CreateQueue(amazonSQSClient, ImageQueueName);
        await CreateQueue(amazonSQSClient, PriorityImageQueueName);
        await CreateQueue(amazonSQSClient, TimebasedQueueName);
        await CreateQueue(amazonSQSClient, TranscodeCompleteQueueName);
        await CreateQueue(amazonSQSClient, FileQueueName);

        var amazonSNSClient = AWSSNSFactory();
        await CreateTopic(amazonSNSClient, AssetModifiedNotificationTopicArn);
    }

    private async Task CreateQueue(IAmazonSQS amazonSQSClient, string queueName)
    {
        var response = await amazonSQSClient.CreateQueueAsync(queueName);
        await amazonSQSClient.SetQueueAttributesAsync(response.QueueUrl, new Dictionary<string, string>
        {
            ["VisibilityTimeout"] = "0"
        });
    }

    private async Task CreateTopic(IAmazonSimpleNotificationService amazonSNSClient, string topicArn)
    {
        var topicName = topicArn.Split(":")[^1];
        _ = await amazonSNSClient.CreateTopicAsync(topicName);
    }
}
