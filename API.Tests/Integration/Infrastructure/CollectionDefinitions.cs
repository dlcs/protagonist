using Test.Helpers.Integration;
using Xunit;

namespace API.Tests.Integration.Infrastructure;

public class CollectionDefinitions
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}