using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration;

/// <summary>
/// Tests zip requests that cannot be tested using localstack
/// This is due to % characters outlined here - https://github.com/localstack/localstack/issues/9112
/// This was tested with localstack version 3.4 and still found to be an issue, but should be revisited in the future.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class ZipTestNoLocalstack : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly IBucketReader bucketReader;
    private readonly FakeZipCreator zipCreator = new();
    
    public ZipTestNoLocalstack(DlcsDatabaseFixture databaseFixture, ProtagonistAppFactory<Startup> factory)
    {
        bucketReader = A.Fake<IBucketReader>();

        dbFixture = databaseFixture;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
                services.AddScoped<IProjectionCreator<ZipParsedNamedQuery>>(_ => zipCreator)
                    .AddSingleton(_ => bucketReader))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();

        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-zip",
            Template = "assetOrder=n2&s1=p1&space=p2&n1=p3&objectname=tester.zip"
        });
        
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/slashes-test"), num1: 1,
            ref1: "ref/with/slashes");
        dbFixture.DbContext.SaveChanges();
    }
    
    [Fact]
    public async Task GetZip_ReturnsZip_WhenPathHasSlashes()
    {
        // Arrange
        const string path = "zip/99/test-zip/ref%2Fwith%2Fslashes/1/1";
        const string zipStorageKey = "99/zip/test-zip/ref%2Fwith%2Fslashes/1/1/tester.zip";
        
        A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(new ObjectInBucket("some-bucket", "some-key"), new MemoryStream(),
                new ObjectInBucketHeaders()));


        List<Asset> savedAssets = null;
        zipCreator.AddCallbackFor(zipStorageKey, (query, assets) =>
        {
            savedAssets = assets;
            return false;
        });
        
        // Act
        await httpClient.GetAsync(path);

        // Assert
        savedAssets.Should().HaveCount(1);
        savedAssets[0].Id.Should().Be(AssetId.FromString("99/1/slashes-test"));
    }
}