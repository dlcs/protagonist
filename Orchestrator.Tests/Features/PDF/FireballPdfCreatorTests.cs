using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using DLCS.Model.Storage;
using FakeItEasy;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Features.PDF;
using Orchestrator.Infrastructure.NamedQueries.PDF;
using Orchestrator.Settings;
using Test.Helpers.Http;
using Xunit;

namespace Orchestrator.Tests.Features.PDF
{
    public class FireballPdfCreatorTests
    {
        private readonly IBucketReader bucketReader;
        private readonly ControllableHttpMessageHandler httpHandler;
        private readonly FireballPdfCreator sut;
        private readonly CustomerPathElement customer = new(99, "Test-Customer");

        public FireballPdfCreatorTests()
        {
            var namedQuerySettings = Options.Create(new NamedQuerySettings
            {
                OutputBucket = "test-pdf-bucket",
                ThumbsBucket = "test-thumbs-bucket"
            });
        
            bucketReader = A.Fake<IBucketReader>();
            
            httpHandler = new ControllableHttpMessageHandler();
            var httpClient = new HttpClient(httpHandler)
            {
                BaseAddress = new Uri("https://fireball")
            };

            sut = new FireballPdfCreator(bucketReader, namedQuerySettings, new NullLogger<FireballPdfCreator>(),
                httpClient);
        }

        [Fact]
        public async Task CreatePdf_False_IfErrorCreatingControlFile()
        {
            // Arrange
            const string controlFileStorageKey = "controlFileKey";
            var parsedNamedQuery = new PdfParsedNamedQuery(customer)
            {
                StorageKey = "pdfKey", ControlFileStorageKey = controlFileStorageKey
            };
            var images = Builder<Asset>.CreateListOfSize(10).Build().ToList();

            A.CallTo(() => bucketReader
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b => b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._))
                .Throws(new Exception());
            
            // Act
            var response = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            response.Should().BeFalse();
            A.CallTo(() => bucketReader
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b => b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._))
                .MustHaveHappened();
        }
        
        [Fact]
        public async Task CreatePdf_False_IfNon2xxHttpResponse_CallingFireball()
        {
            // Arrange
            var parsedNamedQuery = new PdfParsedNamedQuery(customer)
            {
                StorageKey = "pdfKey", ControlFileStorageKey = "controlFileKey"
            };
            var images = Builder<Asset>.CreateListOfSize(10).Build().ToList();

            httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadGateway));
            
            // Act
            var response = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            response.Should().BeFalse();
            httpHandler.CallsMade.Should().Contain("https://fireball/pdf");
        }
        
        [Fact]
        public async Task CreatePdf_False_IfFireballReturnsUnsuccessfulBody()
        {
            // Arrange
            var parsedNamedQuery = new PdfParsedNamedQuery(customer)
            {
                StorageKey = "pdfKey", ControlFileStorageKey = "controlFileKey"
            };
            var images = Builder<Asset>.CreateListOfSize(10).Build().ToList();

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Content =
                new StringContent("{\"success\":false,\"size\":0}", Encoding.UTF8, "application/json");
            httpHandler.SetResponse(responseMessage);
            
            // Act
            var response = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            response.Should().BeFalse();
            httpHandler.CallsMade.Should().Contain("https://fireball/pdf");
        }
        
        [Fact]
        public async Task CreatePdf_UpdatesControlFile()
        {
            // Arrange
            const string controlFileStorageKey = "controlFileKey";
            var parsedNamedQuery = new PdfParsedNamedQuery(customer)
            {
                StorageKey = "pdfKey", ControlFileStorageKey = controlFileStorageKey
            };
            var images = Builder<Asset>.CreateListOfSize(10).Build().ToList();

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Content =
                new StringContent("{\"success\":true,\"size\":100}", Encoding.UTF8, "application/json");
            httpHandler.SetResponse(responseMessage);
            
            // Act
            var response = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            response.Should().BeTrue();
            httpHandler.CallsMade.Should().Contain("https://fireball/pdf");
            A.CallTo(() => bucketReader
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b =>
                            b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._))
                .MustHaveHappened(2, Times.Exactly);
        }
    }
}