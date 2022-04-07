﻿using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Containers.Builders;
using DotNet.Testcontainers.Containers.Modules;
using Xunit;

namespace Test.Helpers.Integration
{
    /// <summary>
    /// Xunit fixture that manages localstack and contains faked AWS clients for interactions.
    /// </summary>
    public class LocalStackFixture : IAsyncLifetime
    {
        private readonly TestcontainersContainer localStackContainer;
        private const int LocalStackContainerPort = 4566;

        public Func<IAmazonS3> AWSS3ClientFactory { get; private set; }

        public LocalStackFixture()
        {
            // Configure container binding to host port 0, which will use a random free port
            var localStackBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("localstack/localstack")
                .WithCleanUp(true)
                .WithLabel("protagonist_test", "True")
                .WithEnvironment("DEFAULT_REGION", "eu-west-1")
                .WithEnvironment("SERVICES", "s3")
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
        }
        
        private async Task SeedAwsResources()
        {
            // Create basic buckets used by DLCS
            var amazonS3Client = AWSS3ClientFactory();
            await amazonS3Client.PutBucketAsync("protagonist-test-origin");
            await amazonS3Client.PutBucketAsync("protagonist-thumbs");
            await amazonS3Client.PutBucketAsync("protagonist-storage");
        }
    }
}