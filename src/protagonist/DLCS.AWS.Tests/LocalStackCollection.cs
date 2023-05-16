using Test.Helpers.Integration;
using Xunit;

namespace DLCS.AWS.Tests;

[CollectionDefinition(CollectionName)]
public class LocalStackCollection : ICollectionFixture<LocalStackFixture>
{
    public const string CollectionName = "LocalStack Collection";
}