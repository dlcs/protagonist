using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;
using Xunit;

namespace Orchestrator.Tests.Integration
{
    /// <summary>
    /// Tests of all zip requests
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(StorageCollection.CollectionName)]
    public class ZipTests: IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;
        private readonly FakeZipCreator zipCreator = new();

        public ZipTests(ProtagonistAppFactory<Startup> factory, StorageFixture orchestratorFixture)
        {
            dbFixture = orchestratorFixture.DbFixture;
            amazonS3 = orchestratorFixture.LocalStackFixture.AWSS3ClientFactory();
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(orchestratorFixture.LocalStackFixture)
                .WithTestServices(services => services.AddScoped<IProjectionCreator<ZipParsedNamedQuery>>(_ => zipCreator))
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
            
            dbFixture.DbContext.NamedQueries.Add(new NamedQuery
            {
                Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-zip",
                Template = "canvas=n2&s1=p1&space=p2&n1=p3&objectname=tester.zip"
            });

            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-zip-1", num1: 2, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-zip-2", num1: 1, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-zip-3-auth", num1: 3, ref1: "my-ref",
                maxUnauthorised: 10, roles: "default");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-zip-4", num1: 4, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-zip-5", num1: 5, ref1: "my-ref");
            dbFixture.DbContext.SaveChanges();
        }
        
        [Fact]
        public async Task GetZip_Returns404_IfCustomerNotFound()
        {
            // Arrange
            const string path = "zip/98/test-zip";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetZip_Returns404_IfNQNotFound()
        {
            // Arrange
            const string path = "zip/99/unknown";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetZip_Returns400_IfParametersIncorrect()
        {
            // Arrange
            const string path = "zip/99/test-zip/too-little-params";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetZip_Returns404_IfNoMatchingRecordsFound()
        {
            // Arrange
            const string path = "zip/99/test-zip/non-matching-ref/2/1";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetZip_Returns202_WithRetryAfter_IfZipInProcess()
        {
            // Arrange
            const string path = "zip/99/test-zip/my-ref/1/1";
            await AddControlFile("99/zip/test-zip/my-ref/1/1/tester.zip.json",
                new ControlFile { Created = DateTime.Now, InProcess = true });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().ContainKey("Retry-After");
        }
        
        [Fact]
        public async Task GetZip_Returns200_WithExistingZip_IfControlFileAndZipExist()
        {
            // Arrange
            var fakeContent = nameof(GetZip_Returns200_WithExistingZip_IfControlFileAndZipExist);
            const string path = "zip/99/test-zip/my-ref/1/1";
            await AddControlFile("99/zip/test-zip/my-ref/1/1/tester.zip.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            await AddZipArchive("99/zip/test-zip/my-ref/1/1/tester.zip", fakeContent);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakeContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/zip"));
        }
        
        [Fact]
        public async Task GetZip_Returns200_WithNewlyCreatedZip_IfControlFileExistsButZipDoesnt()
        {
            // Arrange
            var fakeContent = nameof(GetZip_Returns200_WithNewlyCreatedZip_IfControlFileExistsButZipDoesnt);
            const string storageKey = "99/zip/test-zip/my-ref/1/2/tester.zip";
            const string path = "zip/99/test-zip/my-ref/1/2";
            
            await AddControlFile("99/zip/test-zip/my-ref/1/2/tester.zip.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            zipCreator.AddCallbackFor(storageKey, () =>
            {
                AddZipArchive(storageKey, fakeContent).Wait();
                return true;
            });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakeContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/zip"));
        }
        
        [Fact]
        public async Task GetZip_Returns200_WithNewlyCreateZip_IfZipControlFileStale()
        {
            // Arrange
            var fakeContent = nameof(GetZip_Returns200_WithNewlyCreateZip_IfZipControlFileStale);
            const string storageKey = "99/zip/test-zip/my-ref/1/3/tester.zip";
            const string path = "zip/99/test-zip/my-ref/1/3";
            await AddControlFile("99/zip/test-zip/my-ref/1/3/tester.json",
                new ControlFile { Created = DateTime.Now.AddHours(-1), InProcess = false });
            
            zipCreator.AddCallbackFor(storageKey, () =>
            {
                AddZipArchive(storageKey, fakeContent).Wait();
                return true;
            });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakeContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/zip"));
        }
        
        [Fact]
        public async Task GetZip_Returns500_IfZipCreatedButCannotBeFound()
        {
            // Arrange
            const string path = "zip/99/test-zip/my-ref/1/4";
            const string storageKey = "99/zip/test-zip/my-ref/1/4/tester";
            
            await AddControlFile("99/zip/test-zip/my-ref/1/4/tester.zip.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            
            // return True but don't create object
            zipCreator.AddCallbackFor(storageKey, () => true);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        
        [Fact]
        public async Task GetZip_Returns500_IfZipCreatorUnsuccessful()
        {
            // Arrange
            const string path = "zip/99/test-zip/my-ref/1/5";
            const string storageKey = "99/test-zip/my-ref/1/5/tester";
            
            await AddControlFile("99/test-zip/my-ref/1/5/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            
            zipCreator.AddCallbackFor(storageKey, () => false);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task GetZipControlFile_Returns404_IfCustomerNotFound()
        {
            // Arrange
            const string path = "zip-control/98/test-zip";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetZipControlFile_Returns404_IfNQNotFound()
        {
            // Arrange
            const string path = "zip-control/99/unknown";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetZipControlFile_Returns404_IfParametersIncorrect()
        {
            // Arrange
            const string path = "zip-control/99/test-zip/too-little-params";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetZipControlFile_Returns404_IfNQValidButNoControlFile()
        {
            // Arrange
            const string path = "zip-control/99/test-zip/any-ref/1/2";
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetZipControlFile_Returns200_AndControlFile_IfFound()
        {
            // Arrange
            const string path = "zip-control/99/test-zip/any-ref/1/5";

            var controlFile = new ControlFile
            {
                Created = DateTime.Now, InProcess = false, Exists = true, Key = "the-key", ItemCount = 100,
                SizeBytes = 1024
            };
            await AddControlFile("99/zip/test-zip/any-ref/1/5/tester.zip.json", controlFile);
            var controlFileJson = JsonConvert.SerializeObject(controlFile);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(controlFileJson);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/json"));
        }

        private Task AddControlFile(string key, ControlFile controlFile) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-storage",
                ContentBody = JsonConvert.SerializeObject(controlFile)
            });
        
        private Task AddZipArchive(string key, string fakeContent) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-storage",
                ContentBody = fakeContent
            });
        
        private class FakeZipCreator : IProjectionCreator<ZipParsedNamedQuery>
        {
            private static readonly Dictionary<string, Func<bool>> callbacks = new();

            public void AddCallbackFor(string s3Key, Func<bool> callback)
                => callbacks.Add(s3Key, callback);

            public Task<bool> PersistProjection(ZipParsedNamedQuery parsedNamedQuery, List<Asset> images,
                CancellationToken cancellationToken = default)
            {
                if (callbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cb))
                {
                    return Task.FromResult(cb());
                }

                throw new Exception($"Request with key {parsedNamedQuery.StorageKey} not setup");
            }
        }
    }
}