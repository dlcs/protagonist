using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Repository;
using DLCS.Repository.Entities;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Portal.Tests.Integration.Infrastructure;
using Xunit;

namespace Portal.Tests.Integration
{
    [Trait("Category", "Integration")]
    [Collection(StorageCollection.CollectionName)]
    public class TempTests : IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsContext dbContext;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;

        public TempTests(StorageFixture storageFixture, ProtagonistAppFactory<Startup> factory)
        {
            dbContext = storageFixture.DbFixture.DbContext;
            httpClient = factory
                .WithConnectionString(storageFixture.DbFixture.ConnectionString)
                .WithLocalStack(storageFixture.LocalStackFixture)
                .CreateClient();

            amazonS3 = storageFixture.LocalStackFixture.AmazonS3;
            
            storageFixture.DbFixture.CleanUp();
        }

        [Fact]
        public async Task Test_ControllersCanUseMockedServices()
        {
            // Arrange
            await amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = "test-only",
                BucketName = "protagonist-test-origin",
                ContentBody = "MyContentBody",
            });

            await dbContext.Spaces.AddAsync(new Space {Customer = 1, Id = 10, Created = DateTime.Now, Name = "space1"});
            await dbContext.SaveChangesAsync();

            // Act
            var response = await httpClient.AsCustomer().GetAsync("/temp/index");

            // Assert
            var jsonResponse = JObject.Parse(await response.Content.ReadAsStringAsync());
            jsonResponse.GetValue("spaces").Value<int>().Should().Be(1);
            jsonResponse.GetValue("keys").Value<int>().Should().Be(1);
        }
    }
}