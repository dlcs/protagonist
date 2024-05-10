using Test.Helpers.Integration;

namespace Orchestrator.Tests.Integration.Infrastructure;

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DlcsDatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}

[CollectionDefinition(CollectionName)]
public class LocalStackCollection : ICollectionFixture<LocalStackFixture>
{
    public const string CollectionName = "LocalStack Collection";
}

[CollectionDefinition(CollectionName)]
public class StorageCollection : ICollectionFixture<StorageFixture>
{
    public const string CollectionName = "Storage Collection";
}

[CollectionDefinition(CollectionName)]
public class OrchestratorCollection : ICollectionFixture<OrchestratorFixture>
{
    public const string CollectionName = "Orchestrator Collection";
}