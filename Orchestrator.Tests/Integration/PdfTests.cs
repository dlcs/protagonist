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
    /// Tests of all pdf requests
    /// </summary>
    [Trait("Category", "Integration")]
    [Collection(StorageCollection.CollectionName)]
    public class PdfTests: IClassFixture<ProtagonistAppFactory<Startup>>
    {
        private readonly DlcsDatabaseFixture dbFixture;
        private readonly HttpClient httpClient;
        private readonly IAmazonS3 amazonS3;
        private readonly FakePdfCreator pdfCreator = new();

        public PdfTests(ProtagonistAppFactory<Startup> factory, StorageFixture orchestratorFixture)
        {
            dbFixture = orchestratorFixture.DbFixture;
            amazonS3 = orchestratorFixture.LocalStackFixture.AWSS3ClientFactory();
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(orchestratorFixture.LocalStackFixture)
                .WithTestServices(services => services.AddScoped<IProjectionCreator<PdfParsedNamedQuery>>(_ =>
                {
                    return pdfCreator;
                }))
                .CreateClient(new WebApplicationFactoryClientOptions {AllowAutoRedirect = false});
            
            dbFixture.CleanUp();
            
            dbFixture.DbContext.NamedQueries.Add(new NamedQuery
            {
                Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-pdf",
                Template = "canvas=n2&s1=p1&space=p2&n1=p3&coverpage=https://coverpage.pdf&objectname=tester"
            });

            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-pdf-1", num1: 2, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-pdf-2", num1: 1, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-pdf-3-auth", num1: 3, ref1: "my-ref",
                maxUnauthorised: 10, roles: "default");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-pdf-4", num1: 4, ref1: "my-ref");
            dbFixture.DbContext.Images.AddTestAsset("99/1/matching-pdf-5", num1: 5, ref1: "my-ref");
            dbFixture.DbContext.SaveChanges();
        }
        
        [Fact]
        public async Task GetPdf_Returns404_IfCustomerNotFound()
        {
            // Arrange
            const string path = "pdf/98/test-pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetPdf_Returns404_IfNQNotFound()
        {
            // Arrange
            const string path = "pdf/99/unknown";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetPdf_Returns400_IfParametersIncorrect()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/too-little-params";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetPdf_Returns404_IfNoMatchingRecordsFound()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/non-matching-ref/2/1";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetPdf_Returns202_WithRetryAfter_IfPdfInProcess()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/1";
            await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/1/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = true });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().ContainKey("Retry-After");
        }
        
        [Fact]
        public async Task GetPdf_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist()
        {
            // Arrange
            var fakePdfContent = nameof(GetPdf_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist);
            const string path = "pdf/99/test-pdf/my-ref/1/1";
            await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/1/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            await AddPdf("99/pdf/test-pdf/my-ref/1/1/tester", fakePdfContent);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task GetPdf_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt()
        {
            // Arrange
            var fakePdfContent = nameof(GetPdf_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt);
            const string pdfStorageKey = "99/pdf/test-pdf/my-ref/1/2/tester";
            const string path = "pdf/99/test-pdf/my-ref/1/2";
            
            await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/2/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            pdfCreator.AddCallbackFor(pdfStorageKey, () =>
            {
                AddPdf(pdfStorageKey, fakePdfContent).Wait();
                return true;
            });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task GetPdf_Returns200_WithNewlyCreatedPdf_IfPdfControlFileStale()
        {
            // Arrange
            var fakePdfContent = nameof(GetPdf_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt);
            const string pdfStorageKey = "99/pdf/test-pdf/my-ref/1/3/tester";
            const string path = "pdf/99/test-pdf/my-ref/1/3";
            await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/3/tester.json",
                new ControlFile { Created = DateTime.Now.AddHours(-1), InProcess = false });
            
            pdfCreator.AddCallbackFor(pdfStorageKey, () =>
            {
                AddPdf(pdfStorageKey, fakePdfContent).Wait();
                return true;
            });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task GetPdf_Returns500_IfPdfCreatedButCannotBeFound()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/4";
            const string pdfStorageKey = "99/pdf/test-pdf/my-ref/1/4/tester";
            
            await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/4/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            
            // return True but don't create object
            pdfCreator.AddCallbackFor(pdfStorageKey, () => true);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        
        [Fact]
        public async Task GetPdf_Returns500_IfPdfCreatorUnsuccessful()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/5";
            const string pdfStorageKey = "99/test-pdf/my-ref/1/5/tester";
            
            await AddPdfControlFile("99/test-pdf/my-ref/1/5/tester.json",
                new ControlFile { Created = DateTime.Now, InProcess = false });
            
            pdfCreator.AddCallbackFor(pdfStorageKey, () => false);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task GetPdfControlFile_Returns404_IfCustomerNotFound()
        {
            // Arrange
            const string path = "pdf-control/98/test-pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetPdfControlFile_Returns404_IfNQNotFound()
        {
            // Arrange
            const string path = "pdf-control/99/unknown";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task GetPdfControlFile_Returns404_IfParametersIncorrect()
        {
            // Arrange
            const string path = "pdf-control/99/test-pdf/too-little-params";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetPdfControlFile_Returns404_IfNQValidButNoControlFile()
        {
            // Arrange
            const string path = "pdf-control/99/test-pdf/any-ref/1/2";
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetPdfControlFile_Returns200_AndControlFile_IfFound()
        {
            // Arrange
            const string path = "pdf-control/99/test-pdf/any-ref/1/5";

            var pdfControlFile = new ControlFile
            {
                Created = DateTime.Now, InProcess = false, Exists = true, Key = "the-key", ItemCount = 100,
                SizeBytes = 1024
            };
            await AddPdfControlFile("99/pdf/test-pdf/any-ref/1/5/tester.json", pdfControlFile);
            var pdfControlFileJson = JsonConvert.SerializeObject(pdfControlFile);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(pdfControlFileJson);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/json"));
        }

        private Task AddPdfControlFile(string key, ControlFile controlFile) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-storage",
                ContentBody = JsonConvert.SerializeObject(controlFile)
            });
        
        private Task AddPdf(string key, string fakeContent) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-storage",
                ContentBody = fakeContent
            });
        
        private class FakePdfCreator : IProjectionCreator<PdfParsedNamedQuery>
        {
            private static readonly Dictionary<string, Func<bool>> callbacks = new();

            public void AddCallbackFor(string pdfKey, Func<bool> callback)
                => callbacks.Add(pdfKey, callback);

            public Task<bool> PersistProjection(PdfParsedNamedQuery parsedNamedQuery, List<Asset> images,
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