using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Features.PDF.Requests;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Tests of all pdf requests
/// </summary>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class PdfTests : IClassFixture<ProtagonistAppFactory<Startup>>
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
            .WithTestServices(services =>
                services.AddScoped<IProjectionCreator<PdfParsedNamedQuery>>(_ => pdfCreator))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();

        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-pdf",
            Template = "canvas=n2&s1=p1&space=p2&n1=p3&coverpage=https://coverpage.pdf&objectname=tester"
        });

        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-1"), num1: 2, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-2"), num1: 1, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-3-auth"), num1: 3, ref1: "my-ref",
            maxUnauthorised: 10, roles: "default");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-4"), num1: 4, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-5"), num1: 5, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-6"), num1: 6, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-pdf-6-auth"), num1: 6, ref1: "my-ref",
            maxUnauthorised: 10, roles: "clickthrough");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/not-for-delivery"), num1: 6, ref1: "my-ref",
            notForDelivery: true);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/limited-projection"), num1: 2,
            ref1: "limited-ref");
        dbFixture.DbContext.SaveChanges();
    }

    [Fact]
    public async Task Options_Returns200_WithCorsHeaders()
    {
        // Arrange
        const string path = "pdf/98/test-pdf";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("Access-Control-Allow-Origin");
        response.Headers.Should().ContainKey("Access-Control-Allow-Headers");
        response.Headers.Should().ContainKey("Access-Control-Allow-Methods");
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
    public async Task GetPdf_Returns400_IfNoParameters()
    {
        // Arrange
        const string path = "pdf/99/test-pdf";

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
            new ControlFile { Created = DateTime.UtcNow, InProcess = true });

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Should().ContainKey("Retry-After");
    }
    
    [Fact]
    public async Task GetPdf_Returns200_IfParametersLessThanMax()
    {
        // Arrange
        var fakePdfContent = nameof(GetPdf_Returns200_IfParametersLessThanMax);
        const string path = "pdf/99/test-pdf/limited-ref";
        const string pdfStorageKey = "99/pdf/test-pdf/limited-ref/tester";
        await AddPdfControlFile("99/pdf/test-pdf/limited-ref/tester",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) =>
        {
            AddPdf(pdfStorageKey, fakePdfContent).Wait();
            return true;
        });

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPdf_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist()
    {
        // Arrange
        var fakePdfContent = nameof(GetPdf_Returns200_WithExistingPdf_IfPdfControlFileAndPdfExist);
        const string path = "pdf/99/test-pdf/my-ref/1/1";
        await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/1/tester.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
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
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) =>
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
            new ControlFile { Created = DateTime.UtcNow.AddHours(-1), InProcess = false });

        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) =>
        {
            AddPdf(pdfStorageKey, fakePdfContent).Wait();
            return true;
        });

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.Headers.CacheControl.Public.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(fakePdfContent);
        response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/pdf"));
    }

    [Fact]
    public async Task GetPdf_Returns401_IfControlFileFound_HasRoles_UserCannotAccess()
    {
        // Arrange
        const string path = "pdf/99/test-pdf/my-ref/1/99";
        await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/99/tester.json",
            new ControlFile
            {
                Created = DateTime.UtcNow.AddHours(-1), InProcess = false,
                Roles = new List<string> { "whitelisted-role" }
            });

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPdf_Returns200_WithPdf_IfControlFileFound_HasRoles_AndUserCanAccess()
    {
        // Arrange
        var fakePdfContent = nameof(GetPdf_Returns200_WithPdf_IfControlFileFound_HasRoles_AndUserCanAccess);
        const string pdfStorageKey = "99/pdf/test-pdf/my-ref/1/6/tester";
        const string path = "pdf/99/test-pdf/my-ref/1/6";
        await AddPdfControlFile("99/pdf/test-pdf/my-ref/1/6/tester.json",
            new ControlFile
            {
                Created = DateTime.UtcNow.AddHours(-1), InProcess = false,
                Roles = new List<string> { "clickthrough" }
            });
        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) =>
        {
            AddPdf(pdfStorageKey, fakePdfContent).Wait();
            return true;
        });
        pdfCreator.AddCallbackFor(pdfStorageKey, cf =>
        {
            // Ensure the newly created ControlFile reflects roles
            cf.Roles = new List<string> { "clickthrough" };
            return cf;
        });

        var userSession =
            await dbFixture.DbContext.SessionUsers.AddTestSession(
                DlcsDatabaseFixture.ClickThroughAuthService.AsList());
        var authToken = await dbFixture.DbContext.AuthTokens.AddTestToken(expires: DateTime.UtcNow.AddMinutes(15),
            sessionUserId: userSession.Entity.Id);
        await dbFixture.DbContext.SaveChangesAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", $"dlcs-token-99=id={authToken.Entity.CookieId};");
        var response = await httpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Public.Should().BeFalse();
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
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        // return True but don't create object
        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) => true);

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
        const string pdfStorageKey = "99/pdf/test-pdf/my-ref/1/5/tester";

        await AddPdfControlFile("99/test-pdf/my-ref/1/5/tester.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        pdfCreator.AddCallbackFor(pdfStorageKey, (_, _) => false);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetPdf_CorrectlyOrdersAssets()
    {
        // Arrange
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "ordered-pdf",
            Template = "assetOrder=n1;n2 desc;s1&s2=p1&coverpage=https://coverpage.pdf&objectname=tester"
        });

        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/3"), num1: 1, num2: 10, ref1: "z",
            ref2: "possum");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/1"), num1: 1, num2: 20, ref1: "c",
            ref2: "possum");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/4"), num1: 2, num2: 10, ref1: "a",
            ref2: "possum");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/2"), num1: 1, num2: 10, ref1: "x",
            ref2: "possum");
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedOrder = new[]
        {
            AssetId.FromString("99/1/1"),
            AssetId.FromString("99/1/2"),
            AssetId.FromString("99/1/3"),
            AssetId.FromString("99/1/4")
        };

        const string path = "pdf/99/ordered-pdf/possum";
        const string pdfStorageKey = "99/pdf/ordered-pdf/possum/tester";

        await AddPdfControlFile("99/pdf/ordered-pdf/possum/tester.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        List<Asset> savedAssets = null;
        pdfCreator.AddCallbackFor(pdfStorageKey, (_, assets) =>
        {
            savedAssets = assets;
            return false;
        });

        // Act
        await httpClient.GetAsync(path);

        // Assert
        savedAssets.Select(s => s.Id).Should().BeEquivalentTo(expectedOrder);
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
    public async Task GetPdfControlFile_Returns404_IfNoParameters()
    {
        // Arrange
        const string path = "pdf-control/99/test-pdf";

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("pdf-control/99/test-pdf/any-ref/1/2")]
    [InlineData("pdf-control/99/test-pdf/any-ref")]
    public async Task GetPdfControlFile_Returns200_WithEmptyControlFile_IfNQValidButNoControlFile(string path)
    {
        // Arrange
        var pdfControlFile = new PdfControlFile
        {
            Created = DateTime.MinValue, InProcess = false, Exists = false, Key = string.Empty, ItemCount = 0,
            SizeBytes = 0, Roles = new List<string>(0), PageCount = null
        };
        var pdfControlFileJson = JsonConvert.SerializeObject(pdfControlFile);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(pdfControlFileJson);
    }

    [Fact]
    public async Task GetPdfControlFile_Returns200_AndControlFile_IfFound()
    {
        // Arrange
        const string path = "pdf-control/99/test-pdf/any-ref/1/5";

        var pdfControlFile = new PdfControlFile
        {
            Created = DateTime.UtcNow, InProcess = false, Exists = true, Key = "the-key", ItemCount = 100,
            SizeBytes = 1024
        };
        await AddPdfControlFile("99/pdf/test-pdf/any-ref/1/5/tester.json", pdfControlFile);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/json"));

        var deserialized = JsonConvert.DeserializeObject<PdfControlFile>(await response.Content.ReadAsStringAsync());
        deserialized.Should().BeEquivalentTo(pdfControlFile, opts => opts.Excluding(cf => cf.PageCount));
        deserialized.PageCount.Should().Be(100, "PageCount defaulted from ItemCount if not in JSON");
    }

    [Fact]
    public async Task GetPdfControlFile_Returns200_AndControlFile_IfFound_AndInLegacyFormat()
    {
        // Arrange
        const string path = "pdf-control/99/test-pdf/any-ref/1/5";

        var controlFile = @"{
  ""key"": ""the_key.pdf"",
  ""exists"": true,
  ""inProcess"": false,
  ""created"": ""2021-07-11T00:00:00.000000+00:00"",
  ""roles"": [],
  ""pageCount"": 172,
  ""sizeBytes"": 1024
}";

        await AddPdfControlFile("99/pdf/test-pdf/any-ref/1/5/tester.json", controlFile);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/json"));

        var deserialized = JsonConvert.DeserializeObject<PdfControlFile>(await response.Content.ReadAsStringAsync());
        deserialized.Created.Should().BeCloseTo(new DateTime(2021, 7, 11), TimeSpan.FromHours(6));
        deserialized.InProcess.Should().BeFalse();
        deserialized.Exists.Should().BeTrue();
        deserialized.Key.Should().Be("the_key.pdf");
        deserialized.ItemCount.Should().Be(172, "ItemCount should be set from PageCount");
        deserialized.SizeBytes.Should().Be(1024);
        deserialized.PageCount.Should().Be(172);
    }

    private Task AddPdfControlFile(string key, ControlFile controlFile)
        => amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = key,
            BucketName = LocalStackFixture.OutputBucketName,
            ContentBody = JsonConvert.SerializeObject(controlFile)
        });

    private Task AddPdfControlFile(string key, string controlFile)
        => amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = key,
            BucketName = LocalStackFixture.OutputBucketName,
            ContentBody = controlFile
        });

    private Task AddPdf(string key, string fakeContent)
        => amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = key,
            BucketName = LocalStackFixture.OutputBucketName,
            ContentBody = fakeContent
        });
}
