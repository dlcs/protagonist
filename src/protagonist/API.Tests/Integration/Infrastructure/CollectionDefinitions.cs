using Test.Helpers.Integration;

namespace API.Tests.Integration.Infrastructure;

public class CollectionDefinitions
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}

[CollectionDefinition(CollectionName)]
public class StorageCollection : ICollectionFixture<StorageFixture>
{
    public const string CollectionName = "Storage Collection";
}