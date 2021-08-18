using System;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using FakeItEasy;
using FluentAssertions;
using LazyCache.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Orchestrator.Assets;
using Xunit;

namespace Orchestrator.Tests.Assets
{
    public class MemoryAssetTrackerTests
    {
        private readonly IAssetRepository assetRepository;
        private readonly MemoryAssetTracker sut;

        public MemoryAssetTrackerTests()
        {
            assetRepository = A.Fake<IAssetRepository>();
            sut = new MemoryAssetTracker(assetRepository, new MockCachingService(),
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
        [InlineData('I', typeof(OrchestrationImage))]
        [InlineData('T', typeof(OrchestrationAsset))]
        [InlineData('F', typeof(OrchestrationAsset))]
        public async Task GetOrchestrationAsset_ReturnsCorrectType(char family, Type expectedType)
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
        [InlineData('T')]
        [InlineData('F')]
        public async Task GetOrchestrationAssetT_ReturnsOrchestrationAsset(char family)
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = family });
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationAsset>(assetId);
            
            // Assert
            result.AssetId.Should().Be(assetId);
        }
        
        [Fact]
        public async Task GetOrchestrationAssetT_ReturnsOrchestrationImage()
        {
            // Arrange
            var assetId = new AssetId(1, 1, "go!");
            A.CallTo(() => assetRepository.GetAsset(assetId)).Returns(new Asset { Family = 'I' });
            
            // Act
            var result = await sut.GetOrchestrationAsset<OrchestrationImage>(assetId);
            
            // Assert
            result.AssetId.Should().Be(assetId);
        }
        
        [Theory]
        [InlineData('T')]
        [InlineData('F')]
        public async Task GetOrchestrationAssetT_Null_IfWrongTypeAskedFor(char family)
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