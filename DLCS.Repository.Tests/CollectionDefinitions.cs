using Test.Helpers.Integration;
using Xunit;

namespace DLCS.Repository.Tests
{
    [CollectionDefinition(CollectionName)]
    public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
    {
        public const string CollectionName = "Database Collection";
    }
}