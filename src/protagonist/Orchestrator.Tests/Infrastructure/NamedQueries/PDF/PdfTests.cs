using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using Amazon.S3;
using DLCS.AWS.S3;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Tests.Integration.Infrastructure;
using Test.Helpers.Integration;

namespace Orchestrator.Tests.Infrastructure.NamedQueries.PDF;

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
    private readonly IBucketReader bucketReader;

    public PdfTests(ProtagonistAppFactory<Startup> factory, StorageFixture orchestratorFixture)
    {
        bucketReader = A.Fake<IBucketReader>();

        dbFixture = orchestratorFixture.DbFixture;
        amazonS3 = orchestratorFixture.LocalStackFixture.AWSS3ClientFactory();
        httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
                services.AddScoped<IProjectionCreator<PdfParsedNamedQuery>>(_ => pdfCreator)
                    .AddScoped(_ => bucketReader))
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
        dbFixture.DbContext.SaveChanges();
    }

    [Fact]
    public async Task GetPdf_Returns200_WhenPathHasSlashes()
    {
        // Arrange
        dbFixture.DbContext.Images.AddTestAsset(AssetId.FromString("99/1/slashes-test"), num1: 1,
            ref1: "ref/with/slashes");
        await dbFixture.DbContext.SaveChangesAsync();

        const string path = "pdf-control/99/test-pdf/ref%2Fwith%2Fslashes/1/1";
        const string pdfStorageKey = "99/pdf/test-pdf/ref/with/slashes/1/1";

        // await AddPdfControlFile("99/pdf/test-pdf/ref/with%2Fslashes%2F1/1/tester.json",
        //     new ControlFile { Created = DateTime.UtcNow, InProcess = false });

        List<Asset> savedAssets = null;
        pdfCreator.AddCallbackFor(pdfStorageKey, (query, assets) =>
        {
            savedAssets = assets;
            return false;
        });

        // Act
        var response = await httpClient.GetAsync(path);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        savedAssets.Count().Should().Be(1);
        savedAssets[0].Id.Should().Be(AssetId.FromString("99/1/slashes-test"));
    }

    /// <summary>
    /// Fake projection creator that handles configured callbacks for when ParsedNamedQuery is persisted.
    /// Also optional callback for when ControlFile is created during persistence.
    /// </summary>
    private class FakePdfCreator : IProjectionCreator<PdfParsedNamedQuery>
    {
        private static readonly Dictionary<string, Func<ParsedNamedQuery, List<Asset>, bool>> Callbacks = new();

        private static readonly Dictionary<string, Func<ControlFile, ControlFile>> ControlFileCallbacks = new();

        public void AddCallbackFor(string pdfKey, Func<ParsedNamedQuery, List<Asset>, bool> callback)
            => Callbacks.Add(pdfKey, callback);

        public void AddCallbackFor(string pdfKey, Func<ControlFile, ControlFile> callback)
            => ControlFileCallbacks.Add(pdfKey, callback);

        public Task<(bool success, ControlFile controlFile)> PersistProjection(PdfParsedNamedQuery parsedNamedQuery,
            List<Asset> images, CancellationToken cancellationToken = default)
        {
            if (Callbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cb))
            {
                var controlFileCallback = ControlFileCallbacks.TryGetValue(parsedNamedQuery.StorageKey, out var cfcb)
                    ? cfcb
                    : file => file;

                return Task.FromResult((cb(parsedNamedQuery, images), controlFileCallback(new ControlFile())));
            }

            throw new Exception($"Request with key {parsedNamedQuery.StorageKey} not setup");
        }
    }
}