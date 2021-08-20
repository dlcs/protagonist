using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Customer;
using DLCS.Repository.Strategy;
using FluentAssertions;
using Xunit;

namespace DLCS.Repository.Tests.Strategy
{
    public class SafetyCheckOriginStrategyTests
    {
        private readonly TestStrategy sut;
        private readonly AssetId assetId = new(2, 2, "foo");

        public SafetyCheckOriginStrategyTests()
        {
            sut = new TestStrategy();
        }
        
        [Fact]
        public void LoadAssetFromOrigin_Throws_IfTokenCancelled()
        {
            // Act
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Func<Task> action = () => sut.LoadAssetFromOrigin(assetId, "origin", new CustomerOriginStrategy(), cts.Token);
            
            // Assert
            action.Should()
                .Throw<OperationCanceledException>();
        }
        
        [Fact]
        public void LoadAssetFromOrigin_Throws_IfCustomerOriginStrategyNull()
        {
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(assetId, "origin", null);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'customerOriginStrategy')");
        }

        [Fact]
        public void LoadAssetFromOrigin_Throws_IfAssetNull()
        {
            // Arrange
            var customerOriginStrategy = new CustomerOriginStrategy {Strategy = OriginStrategyType.S3Ambient};
            
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(null, "origin", customerOriginStrategy);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'assetId')");
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void LoadAssetFromOrigin_Throws_IfOriginNullOrWhitespace(string origin)
        {
            // Arrange
            var customerOriginStrategy = new CustomerOriginStrategy {Strategy = OriginStrategyType.S3Ambient};
            
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(assetId, origin, customerOriginStrategy);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'origin')");
        }
        
        private class TestStrategy : SafetyCheckOriginStrategy
        {
            public bool HaveBeenCalled { get; private set; }

            protected override Task<OriginResponse?> LoadAssetFromOriginImpl(AssetId asset, string origin,
                CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
            {
                HaveBeenCalled = true;
                return Task.FromResult(new OriginResponse(Stream.Null));
            }
        }
    }
}