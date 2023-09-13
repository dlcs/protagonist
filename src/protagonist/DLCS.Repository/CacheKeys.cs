namespace DLCS.Repository;

/// <summary>
/// Class for managing commonly used cache keys
/// </summary>
public static class CacheKeys
{
    public static string Customer(int customerId) => $"cust:{customerId}";
}