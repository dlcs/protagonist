using System.Collections.Generic;
using System.Threading;
using DLCS.Core.Types;
using DLCS.Repository.Assets;
using FakeItEasy;
using IIIF;
using IIIF.Auth.V2;
using IIIF.ImageApi;
using IIIF.ImageApi.V3;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Assets;
using Orchestrator.Features.Images.ImageServer;
using Orchestrator.Infrastructure.IIIF;
using Orchestrator.Tests.Integration;

namespace Orchestrator.Tests.Features.Images.ImageServerTests;

public class InfoJson3ConstructorTest : InfoJson3Constructor
{
    private static readonly IIIIFAuthBuilder IiifAuthBuilder = A.Fake<IIIIFAuthBuilder>();
    private static readonly ThumbRepository ThumbRepository = A.Fake<ThumbRepository>();

    public InfoJson3ConstructorTest() : base(IiifAuthBuilder, new FakeImageServerClient(), ThumbRepository,
        new NullLogger<InfoJson3Constructor>())
    {
    }

    [Theory]
    [InlineData(512)]
    [InlineData(256)]
    public void SetImageTileServiceSizes_UpdatesTileSizeIfNeeded(int tileSize)
    {
        // Arrange
        var imageService = new ImageService3
        {
            Tiles = new List<Tile>{new Tile()
            {
                Height = tileSize,
                Width = tileSize
            }}
        };

        // Act
        SetImageTileServiceSizes(imageService, 500);

        // Assert
        imageService.Tiles[0].Width.Should().Be(256);
        imageService.Tiles[0].Height.Should().Be(256);
    }
    
    [Fact]
    public void SetImageTileServiceSizes_UpdatesSize()
    {
        // Arrange
        var imageService = new ImageService3
        {
            Sizes = new List<Size> { new Size(256, 256) }
        };

        var newSizes = new List<Size> { new Size(512, 512) };
        

        // Act
        SetImageServiceSizes(imageService, newSizes);

        // Assert
        imageService.Sizes[0].Width.Should().Be(512);
        imageService.Sizes[0].Height.Should().Be(512);
    }
    
    [Fact]
    public void SetImageServiceStubId_UpdatesServiceStubId()
    {
        // Arrange
        var imageService = new ImageService3
        {
            Id = "someId"
        };

        var orchestrationImage = new OrchestrationImage();
        orchestrationImage.AssetId = new AssetId(1, 1, "someAsset");
        
        // Act
        SetImageServiceStubId(imageService, orchestrationImage);

        // Assert
        imageService.Id.Should().Be("v3/1/1/someAsset");
    }
    
    [Fact]
    public void SetImageServiceMaxWidth_UpdatesMaxWidth()
    {
        // Arrange
        var imageService = new ImageService3
        {
            MaxArea = 500,
            MaxHeight = 500,
            MaxWidth = 500
        };

        var orchestrationImage = new OrchestrationImage();
        orchestrationImage.MaxUnauthorised = 250;
        
        // Act
        SetImageServiceMaxWidth(imageService, orchestrationImage);

        // Assert
        imageService.MaxArea.Should().BeNull();
        imageService.MaxHeight.Should().BeNull();
        imageService.MaxWidth.Should().Be(250);
    }
    
    [Fact]
    public async Task SetImageServiceAuthServices_UpdatesAuthService()
    {
        // Arrange
        var imageService = new ImageService3();

        var authService = new AuthProbeService2();

        A.CallTo(() => IiifAuthBuilder.GetAuthServicesForAsset(A<AssetId>._, A<List<string>>._, A<CancellationToken>._))
            .Returns(authService);

        // Act
        await SetImageServiceAuthServices(imageService, new OrchestrationImage(), new CancellationToken());

        // Assert
        imageService.Service.Count.Should().Be(1);
    }
}