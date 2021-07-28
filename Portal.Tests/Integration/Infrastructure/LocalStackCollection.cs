using Test.Helpers.Integration;
using Xunit;

namespace Portal.Tests.Integration.Infrastructure
{
    [CollectionDefinition(CollectionName)]
    public class LocalStackCollection : ICollectionFixture<LocalStackFixture>
    {
        public const string CollectionName = "LocalStack Collection";
    }
}