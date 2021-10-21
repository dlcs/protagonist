using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Features.PDF;
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
        private readonly IPdfCreator pdfCreator;

        public PdfTests(ProtagonistAppFactory<Startup> factory, StorageFixture orchestratorFixture)
        {
            pdfCreator = A.Fake<IPdfCreator>();
            dbFixture = orchestratorFixture.DbFixture;
            amazonS3 = orchestratorFixture.LocalStackFixture.AmazonS3;
            httpClient = factory
                .WithConnectionString(dbFixture.ConnectionString)
                .WithLocalStack(orchestratorFixture.LocalStackFixture)
                .WithTestServices(services => services.AddTransient<IPdfCreator>(_ => pdfCreator))
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
            dbFixture.DbContext.SaveChanges();
        }
        
        [Fact]
        public async Task Get_Returns404_IfCustomerNotFound()
        {
            // Arrange
            const string path = "pdf/98/test-pdf";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_Returns404_IfNQNotFound()
        {
            // Arrange
            const string path = "pdf/99/unknown";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_Returns400_IfParametersIncorrect()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/too-little-params";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Get_Returns404_IfNoMatchingRecordsFound()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/non-matching-ref/2/1";

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        
        [Fact]
        public async Task Get_Returns202_WithRetryAfter_IfPdfInProcess()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/1";
            await AddPdfControlFile("99/test-pdf/my-ref/1/1/tester.json",
                new PdfControlFile { Created = DateTime.Now, InProcess = true });

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.Should().ContainKey("Retry-After");
        }
        
        [Fact]
        public async Task Get_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist()
        {
            // Arrange
            var fakePdfContent = nameof(Get_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist);
            const string path = "pdf/99/test-pdf/my-ref/1/1";
            await AddPdfControlFile("99/test-pdf/my-ref/1/1/tester.json",
                new PdfControlFile { Created = DateTime.Now, InProcess = false });
            await AddPdf("99/test-pdf/my-ref/1/1/tester", fakePdfContent);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task Get_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt()
        {
            // Arrange
            var fakePdfContent = nameof(Get_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt);
            const string pdfStorageKey = "99/test-pdf/my-ref/1/2/tester";
            const string path = "pdf/99/test-pdf/my-ref/1/2";
            
            await AddPdfControlFile("99/test-pdf/my-ref/1/2/tester.json",
                new PdfControlFile { Created = DateTime.Now, InProcess = false });
            A.CallTo(() =>
                    pdfCreator.CreatePdf(
                        A<PdfParsedNamedQuery>.That.Matches(p => p.PdfStorageKey == pdfStorageKey),
                        A<List<Asset>>._))
                .Invokes(() => AddPdf(pdfStorageKey, fakePdfContent).Wait())
                .Returns(true);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task Get_Returns200_WithNewlyCreatedPdf_IfPdfControlFileStale()
        {
            // Arrange
            var fakePdfContent = nameof(Get_Returns200_WithNewlyCreatedPdf_IfPdfControlFileExistsButPdfDoesnt);
            const string pdfStorageKey = "99/test-pdf/my-ref/1/3/tester";
            const string path = "pdf/99/test-pdf/my-ref/1/3";
            await AddPdfControlFile("99/test-pdf/my-ref/1/3/tester.json",
                new PdfControlFile { Created = DateTime.Now.AddHours(-1), InProcess = false });
            
            A.CallTo(() =>
                    pdfCreator.CreatePdf(
                        A<PdfParsedNamedQuery>.That.Matches(p => p.PdfStorageKey == pdfStorageKey),
                        A<List<Asset>>._))
                .Invokes(() => AddPdf(pdfStorageKey, fakePdfContent).Wait())
                .Returns(true);

            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
            response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
        }
        
        [Fact]
        public async Task Get_Returns500_IfPdfCreatedButCannotBeFound()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/4";
            const string pdfStorageKey = "99/test-pdf/my-ref/1/4/tester";
            
            await AddPdfControlFile("99/test-pdf/my-ref/1/4/tester.json",
                new PdfControlFile { Created = DateTime.Now, InProcess = false });
            
            // return True but don't create object
            A.CallTo(() =>
                    pdfCreator.CreatePdf(
                        A<PdfParsedNamedQuery>.That.Matches(p => p.PdfStorageKey == pdfStorageKey),
                        A<List<Asset>>._))
                .Returns(true);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        
        [Fact]
        public async Task Get_Returns500_IfPdfCreatorUnsuccessful()
        {
            // Arrange
            const string path = "pdf/99/test-pdf/my-ref/1/4";
            const string pdfStorageKey = "99/test-pdf/my-ref/1/4/tester";
            
            await AddPdfControlFile("99/test-pdf/my-ref/1/4/tester.json",
                new PdfControlFile { Created = DateTime.Now, InProcess = false });
            
            A.CallTo(() =>
                    pdfCreator.CreatePdf(
                        A<PdfParsedNamedQuery>.That.Matches(p => p.PdfStorageKey == pdfStorageKey),
                        A<List<Asset>>._))
                .Returns(false);
            
            // Act
            var response = await httpClient.GetAsync(path);
            
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        private Task AddPdfControlFile(string key, PdfControlFile controlFile) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-pdf",
                ContentBody = JsonConvert.SerializeObject(controlFile)
            });
        
        private Task AddPdf(string key, string fakeContent) 
            => amazonS3.PutObjectAsync(new PutObjectRequest
            {
                Key = key,
                BucketName = "protagonist-pdf",
                ContentBody = fakeContent
            });
    }
}