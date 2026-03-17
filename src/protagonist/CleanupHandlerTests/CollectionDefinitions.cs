using Test.Helpers.Integration;

namespace DeleteHandlerTests;

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}
