using Test.Helpers.Integration;

namespace DLCS.Repository.Tests;

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}