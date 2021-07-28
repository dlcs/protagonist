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
        
        // LocalStack url
        private const string LocalStackUrl = "http://localhost:4566/";

        public IAmazonS3 AmazonS3 { get; }
        
        public LocalStackFixture()
        {
            var localStackBuilder = new TestcontainersBuilder<TestcontainersContainer>()
                .WithImage("localstack/localstack")
                .WithCleanUp(true)
                .WithLabel("protagonist_test", "True")
                .WithEnvironment("DEFAULT_REGION", "eu-west-1")
                .WithEnvironment("SERVICES", "s3")
                .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
                .WithEnvironment("DEBUG", "1")
                .WithPortBinding(4566, 4566);

            localStackContainer = localStackBuilder.Build();
            
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.EUWest1,
                UseHttp = true,
                ForcePathStyle = true,
                ServiceURL = LocalStackUrl
            };
                    
            AmazonS3 = new AmazonS3Client(new BasicAWSCredentials("foo", "bar"), s3Config);
        }

        public async Task InitializeAsync()
        {
            // Start local stack + create any required resources
            await localStackContainer.StartAsync();
            await SeedAwsResources();
        }

        public Task DisposeAsync() => localStackContainer.StopAsync();
        
        private async Task SeedAwsResources()
        {
            // Create basic buckets used by DLCS
            await AmazonS3.PutBucketAsync("protagonist-test-origin");
        }
    }
}