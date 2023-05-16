﻿using Test.Helpers.Integration;
using Xunit;

namespace Portal.Tests.Integration.Infrastructure;

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