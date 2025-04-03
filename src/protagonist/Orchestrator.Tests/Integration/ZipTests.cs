﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Amazon.S3;
using Amazon.S3.Model;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Tests of all zip requests
/// </summary>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class ZipTests : IClassFixture<ProtagonistAppFactory<Startup>>
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
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();

        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-zip",
            Template = "assetOrder=n2&s1=p1&space=p2&n1=p3&objectname=tester.zip"
        });

        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-zip-1"), num1: 2, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-zip-2"), num1: 1, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-zip-3-auth"), num1: 3, ref1: "my-ref",
            maxUnauthorised: 10, roles: "default");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-zip-4"), num1: 4, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/matching-zip-5"), num1: 5, ref1: "my-ref");
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/not-for-delivery"), num1: 6, ref1: "my-ref",
            notForDelivery: true);
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/limited-parameter-zip-1"), num1: 2,
            ref1: "limited-ref");
        dbFixture.DbContext.SaveChanges();
    }

    [Fact]
    public async Task Options_Returns200_WithCorsHeaders()
    {
        // Arrange
        const string path = "zip/98/test-zip";

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
            new ControlFile { Created = DateTime.UtcNow, InProcess = true });

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
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
        await AddZipArchive("99/zip/test-zip/my-ref/1/1/tester.zip", fakeContent);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(fakeContent);
        response.Content.Headers.ContentType.Should().Be(new MediaTypeHeaderValue("application/zip"));
    }
    
    [Fact]
    public async Task GetZip_Returns200_IfLessParametersThanTotal()
    {
        // Arrange
        var fakeContent = nameof(GetZip_Returns200_IfLessParametersThanTotal);
        const string path = "zip/99/test-zip/limited-ref";
        await AddControlFile("99/zip/test-zip/limited-ref/tester.zip.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
        await AddZipArchive("99/zip/test-zip/limited-ref/tester.zip", fakeContent);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetZip_Returns200_WithNewlyCreatedZip_IfControlFileExistsButZipDoesnt()
    {
        // Arrange
        var fakeContent = nameof(GetZip_Returns200_WithNewlyCreatedZip_IfControlFileExistsButZipDoesnt);
        const string storageKey = "99/zip/test-zip/my-ref/1/2/tester.zip";
        const string path = "zip/99/test-zip/my-ref/1/2";

        await AddControlFile("99/zip/test-zip/my-ref/1/2/tester.zip.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });
        zipCreator.AddCallbackFor(storageKey, (_, _) =>
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
            new ControlFile { Created = DateTime.UtcNow.AddHours(-1), InProcess = false });

        zipCreator.AddCallbackFor(storageKey, (_, _) =>
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
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        // return True but don't create object in s3
        zipCreator.AddCallbackFor(storageKey, (_, _) => true);

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
        const string storageKey = "99/zip/test-zip/my-ref/1/5/tester";

        await AddControlFile("99/test-zip/my-ref/1/5/tester.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        zipCreator.AddCallbackFor(storageKey, (_, _) => false);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetZip_CorrectlyOrdersAssets()
    {
        // Arrange
        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "ordered-zip",
            Template = "assetOrder=n1;n2 desc;s1&s2=p1&objectname=tester"
        });

        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/third"), num1: 1, num2: 10, ref1: "z",
            ref2: "ordered");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/first"), num1: 1, num2: 20, ref1: "c",
            ref2: "ordered");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/fourth"), num1: 2, num2: 10, ref1: "a",
            ref2: "ordered");
        await dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/second"), num1: 1, num2: 10, ref1: "x",
            ref2: "ordered");
        await dbFixture.DbContext.SaveChangesAsync();

        var expectedOrder = new[]
        {
            AssetId.FromString("99/1/first"),
            AssetId.FromString("99/1/second"),
            AssetId.FromString("99/1/third"),
            AssetId.FromString("99/1/fourth"),
        };

        const string path = "zip/99/ordered-zip/ordered";
        const string storageKey = "99/zip/ordered-zip/ordered/tester";

        await AddControlFile("99/ordered-zip/ordered/tester.json",
            new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        List<Asset> savedAssets = null;
        zipCreator.AddCallbackFor(storageKey, (_, assets) =>
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

    [Theory]
    [InlineData("zip-control/99/test-zip/any-ref/1/2")]
    [InlineData("zip-control/99/test-zip/any-ref")]
    public async Task GetZipControlFile_Returns200_WithEmptyControlFile_IfNQValidButNoControlFile(string path)
    {
        // Arrange
        var controlFileJson = JsonConvert.SerializeObject(ControlFile.Empty);

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(controlFileJson);
    }

    [Fact]
    public async Task GetZipControlFile_Returns200_AndControlFile_IfFound()
    {
        // Arrange
        const string path = "zip-control/99/test-zip/any-ref/1/5";

        var controlFile = new ControlFile
        {
            Created = DateTime.UtcNow, InProcess = false, Exists = true, Key = "the-key", ItemCount = 100,
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
            BucketName = "protagonist-output",
            ContentBody = JsonConvert.SerializeObject(controlFile)
        });

    private Task AddZipArchive(string key, string fakeContent)
        => amazonS3.PutObjectAsync(new PutObjectRequest
        {
            Key = key,
            BucketName = "protagonist-output",
            ContentBody = fakeContent
        });
}
