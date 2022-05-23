﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository.Caching;
using FakeItEasy;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images.Orchestration.Status;
using Xunit;

namespace Orchestrator.Tests.Assets
{
    public class MemoryAssetTrackerTests
    {
        private readonly IAssetRepository assetRepository;
        private readonly IThumbRepository thumbRepository;
        private readonly MemoryAssetTracker sut;
        private readonly IImageOrchestrationStatusProvider imageOrchestrationStatusProvider;

        public MemoryAssetTrackerTests()
        {
            assetRepository = A.Fake<IAssetRepository>();
            thumbRepository = A.Fake<IThumbRepository>();
            imageOrchestrationStatusProvider = A.Fake<IImageOrchestrationStatusProvider>();

            sut = new MemoryAssetTracker(assetRepository, new MockCachingService(), thumbRepository,
                imageOrchestrationStatusProvider, Options.Create(new CacheSettings()),
                new NullLogger<MemoryAssetTracker>());
        }

        [Fact]
        public async Task GetOrchestrationAsset_Null_IfNotFound()
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
            
            // Act
            var result = await sut.GetOrchestrationAsset(assetId);
            
            // Assert
            result.Should().BeNull();
        }

        [Theory]
        [InlineData(AssetFamily.Image, typeof(OrchestrationImage))]
        //[InlineData('T', typeof(OrchestrationAsset))]
        //[InlineData('F', typeof(OrchestrationFile))]
        public async Task GetOrchestrationAsset_ReturnsCorrectType(AssetFamily family, Type expectedType)
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
            
            // Act
            var result = await sut.GetOrchestrationAsset(assetId);
            
            // Assert
            result.AssetId.Should().Be(assetId);
            result.Should().BeOfType(expectedType);
        }
        
        [Fact]
        public async Task GetOrchestrationAssetT_Null_IfOrchestrationAssetNotFound()
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
            
            // Assert
            result.Should().BeNull();
        }
        
        [Fact]
        public async Task GetOrchestrationAssetT_Null_IfOrchestrationImageNotFound()
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns<Asset>(null);
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
            
            // Assert
            result.Should().BeNull();
        }
        
        [Theory]
        [InlineData(AssetFamily.Timebased)]
        [InlineData(AssetFamily.File)]
        public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset(AssetFamily family)
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
            
            // Assert
            result.AssetId.Should().Be(assetId);
            A.CallTo(() => thumbRepository.GetOpenSizes(A<AssetId>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async Task GetOrchestrationAssetT_ReturnsOrchestrationImage()
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            var sizes = new List<int[]> { new[] { 100, 200 } };
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = AssetFamily.Image });
            A.CallTo(() => thumbRepository.GetOpenSizes(assetId)).Returns(sizes);

            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
            
            // Assert
            result.AssetId.Should().Be(assetId);
            result.OpenThumbs.Should().BeEquivalentTo(sizes);
        }
        
        [Theory]
        [InlineData(AssetFamily.Timebased)]
        [InlineData(AssetFamily.File)]
        public async Task GetOrchestrationAssetT_Null_IfWrongTypeAskedFor(AssetFamily family)
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
            
            // Assert
            result.Should().BeNull();
        }
    }
}