using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
            Func<Task> action = () => sut.LoadAssetFromOrigin(new Asset(), new CustomerOriginStrategy(), cts.Token);
            
            // Assert
            action.Should()
                .Throw<OperationCanceledException>();
        }
        
        [Fact]
        public void LoadAssetFromOrigin_Throws_IfCustomerOriginStrategyNull()
        {
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(new Asset(), null);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'customerOriginStrategy')");
        }
        
        [Theory]
        [InlineData(OriginStrategyType.Default)]
        [InlineData(OriginStrategyType.BasicHttp)]
        [InlineData(OriginStrategyType.SFTP)]
        public void LoadAssetFromOrigin_Throws_IfCustomerOriginStrategyDiffersFromImplementationStrategy(OriginStrategyType strategy)
        {
            // Arrange
            var customerOriginStrategy = new CustomerOriginStrategy {Strategy = strategy};
            
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(new Asset(), customerOriginStrategy);
            
            // Assert
            action.Should()
                .Throw<InvalidOperationException>();
        }
        
        [Fact]
        public void LoadAssetFromOrigin_Throws_IfAssetNull()
        {
            // Arrange
            var customerOriginStrategy = new CustomerOriginStrategy {Strategy = OriginStrategyType.S3Ambient};
            
            // Act
            Func<Task> action = () => sut.LoadAssetFromOrigin(null, customerOriginStrategy);
            
            // Assert
            action.Should()
                .Throw<ArgumentNullException>()
                .WithMessage("Value cannot be null. (Parameter 'asset')");
        }
        
        [Fact]
        public async Task LoadAssetFromOrigin_CallsImplementation_IfAssetNull()
        {
            // Arrange
            var customerOriginStrategy = new CustomerOriginStrategy {Strategy = OriginStrategyType.S3Ambient};
            
            // Act
            await sut.LoadAssetFromOrigin(new Asset(), customerOriginStrategy);
            
            // Assert
            sut.HaveBeenCalled.Should().BeTrue();
        }
        
        private class TestStrategy : SafetyCheckOriginStrategy
        {
            public override OriginStrategyType Strategy => OriginStrategyType.S3Ambient;
            
            public bool HaveBeenCalled { get; private set; }

            protected override Task<OriginResponse?> LoadAssetFromOriginImpl(Asset asset,
                CustomerOriginStrategy customerOriginStrategy, CancellationToken cancellationToken = default)
            {
                HaveBeenCalled = true;
                return Task.FromResult(new OriginResponse(Stream.Null));
            }
        }
    }
}