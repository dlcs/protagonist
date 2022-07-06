using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.AWS.Settings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.PathElements;
using FakeItEasy;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.PDF;
using Orchestrator.Settings;
using Test.Helpers.Http;
using Xunit;

namespace Orchestrator.Tests.Infrastructure.NamedQueries.PDF
{
    public class FireballPdfCreatorTests
    {
        private readonly IBucketReader bucketReader;
        private readonly ControllableHttpMessageHandler httpHandler;
        private readonly FireballPdfCreator sut;
        private readonly CustomerPathElement customer = new(99, "Test-Customer");
        private readonly IBucketWriter bucketWriter;

        public FireballPdfCreatorTests()
        {
            var namedQuerySettings = Options.Create(new NamedQuerySettings
            {
                CustomerOverrides = new Dictionary<string, CustomerOverride>
                {
                    ["99"] = new()
                    {
                        PdfRolesWhitelist = new List<string> { "whitelist" }
                    }
                }
            });
        
            bucketReader = A.Fake<IBucketReader>();
            bucketWriter = A.Fake<IBucketWriter>();
            
            httpHandler = new ControllableHttpMessageHandler();
            var httpClient = new HttpClient(httpHandler)
            {
                BaseAddress = new Uri("https://fireball")
            };

            var bucketKeyGenerator =
                new S3StorageKeyGenerator(Options.Create(new AWSSettings
                {
                    S3 = new S3Settings
                    {

                        OutputBucket = "test-pdf-bucket",
                        ThumbsBucket = "test-thumbs-bucket"
                    }
                }));

            sut = new FireballPdfCreator(bucketReader, bucketWriter, namedQuerySettings,
                new NullLogger<FireballPdfCreator>(), httpClient, bucketKeyGenerator);
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

            A.CallTo(() => bucketWriter
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b => b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._, A<CancellationToken>._))
                .Throws(new Exception());
            
            // Act
            var (success, _) = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            success.Should().BeFalse();
            A.CallTo(() => bucketWriter
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b => b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._, A<CancellationToken>._))
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
            var images = Builder<Asset>
                .CreateListOfSize(10)
                .All()
                .With(a => a.Id = $"/{a.Customer}/{a.Space}/{a.Origin}")
                .Build()
                .ToList();

            httpHandler.SetResponse(new HttpResponseMessage(HttpStatusCode.BadGateway));
            
            // Act
            var (success, _) = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            success.Should().BeFalse();
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
            var images = Builder<Asset>
                .CreateListOfSize(10)
                .All()
                .With(a => a.Id = $"/{a.Customer}/{a.Space}/{a.Origin}")
                .Build()
                .ToList();

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Content =
                new StringContent("{\"success\":false,\"size\":0}", Encoding.UTF8, "application/json");
            httpHandler.SetResponse(responseMessage);
            
            // Act
            var (success, _) = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            success.Should().BeFalse();
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
            var images = Builder<Asset>
                .CreateListOfSize(10)
                .All()
                .With(a => a.Id = $"/{a.Customer}/{a.Space}/{a.Origin}")
                .Build()
                .ToList();

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Content =
                new StringContent("{\"success\":true,\"size\":100}", Encoding.UTF8, "application/json");
            httpHandler.SetResponse(responseMessage);
            
            // Act
            var (success, _) = await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            success.Should().BeTrue();
            httpHandler.CallsMade.Should().Contain("https://fireball/pdf");
            A.CallTo(() => bucketWriter
                    .WriteToBucket(
                        A<ObjectInBucket>.That.Matches(b =>
                            b.Key == controlFileStorageKey && b.Bucket == "test-pdf-bucket"),
                        A<string>._, A<string>._, A<CancellationToken>._))
                .MustHaveHappened(2, Times.Exactly);
        }
        
        [Fact]
        public async Task CreatePdf_RedactsNotWhitelistedRoles()
        {
            // Arrange
            var parsedNamedQuery = new PdfParsedNamedQuery(customer)
            {
                StorageKey = "pdfKey", ControlFileStorageKey = "controlFileKey"
            };
            var images = Builder<Asset>
                .CreateListOfSize(5)
                .TheFirst(1).With(a => a.Roles = "whitelist")
                .TheNext(1).With(a => a.Roles = "whitelist,notwhitelist")
                .TheNext(1).With(a => a.Roles = "notwhitelist")
                .All()
                .With(a => a.Id = $"/{a.Customer}/{a.Space}/{a.Origin}")
                .Build()
                .ToList();

            var responseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            responseMessage.Content =
                new StringContent("{\"success\":false,\"size\":0}", Encoding.UTF8, "application/json");
            httpHandler.SetResponse(responseMessage);
            
            FireballPlaybook playbook = null;
            httpHandler.RegisterCallback(message =>
                playbook = message.Content.ReadFromJsonAsync<FireballPlaybook>().Result);

            var expectedPageTypes = new[] { "pdf", "jpg", "redacted", "redacted", "jpg", "jpg" };
            
            // Act
            await sut.PersistProjection(parsedNamedQuery, images);
            
            // Assert
            playbook.Pages.Select(p => p.Type).Should()
                .BeEquivalentTo(expectedPageTypes, opts => opts.WithStrictOrdering());
        }
    }
}