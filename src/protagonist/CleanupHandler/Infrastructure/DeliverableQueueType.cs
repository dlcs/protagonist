namespace CleanupHandler.Infrastructure;

// These are the same, but this avoids issues when the services are registered

public enum AssetQueueType
{
    Delete,
    Update
}

public enum AdjunctQueueType
{
    Delete,
    Update
}
