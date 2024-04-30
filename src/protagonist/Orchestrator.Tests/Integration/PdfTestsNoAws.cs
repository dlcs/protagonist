using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// Tests pdf requests that cannot be tested using localstack
/// </summary>
[Trait("Category", "Integration")]
[Collection(StorageCollection.CollectionName)]
public class PdfTestsNoAws : IClassFixture<ProtagonistAppFactory<Startup>>
{
    private readonly DlcsDatabaseFixture dbFixture;
    private readonly HttpClient httpClient;
    private readonly FakePdfCreator pdfCreator = new();
    private readonly IBucketReader bucketReader;

    public PdfTestsNoAws(ProtagonistAppFactory<Startup> factory, StorageFixture orchestratorFixture)
    {
        bucketReader = A.Fake<IBucketReader>();

        dbFixture = orchestratorFixture.DbFixture;
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
                services.AddScoped<IProjectionCreator<PdfParsedNamedQuery>>(_ => pdfCreator)
                    .AddSingleton(_ => bucketReader))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        dbFixture.CleanUp();

        dbFixture.DbContext.NamedQueries.Add(new NamedQuery
        {
            Customer = 99, Global = false, Id = Guid.NewGuid().ToString(), Name = "test-pdf",
            Template = "canvas=n2&s1=p1&space=p2&n1=p3&coverpage=https://coverpage.pdf&objectname=tester"
        });
        
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/slashes-test"), num1: 1,
            ref1: "ref/with/slashes");
        dbFixture.DbContext.SaveChanges();
    }

    [Fact]
    public async Task GetPdf_Returns200_WhenPathHasSlashes()
    {
        // Arrange
        const string path = "pdf/99/test-pdf/ref%2Fwith%2Fslashes/1/1";
        const string pdfStorageKey = "99/pdf/test-pdf/ref%2Fwith%2Fslashes/1/1/tester";
        
        A.CallTo(() => bucketReader.GetObjectFromBucket(A<ObjectInBucket>._, A<CancellationToken>._))
            .Returns(new ObjectFromBucket(new ObjectInBucket("some-bucket", "some-key"), new MemoryStream(),
                new ObjectInBucketHeaders()));


        List<Asset> savedAssets = null;
        pdfCreator.AddCallbackFor(pdfStorageKey, (query, assets) =>
        {
            savedAssets = assets;
            return false;
        });

        // Act
        await httpClient.GetAsync(path);

        // Assert
        savedAssets.Count().Should().Be(1);
        savedAssets[0].Id.Should().Be(AssetId.FromString("99/1/slashes-test"));
    }
}