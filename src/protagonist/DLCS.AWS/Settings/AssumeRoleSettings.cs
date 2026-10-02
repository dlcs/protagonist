namespace DLCS.AWS.Settings;

/// <summary>
/// Settings controlling whether AWS clients are created for a specific customer, using an assumed role that carries
/// the customer as a session tag.
/// </summary>
/// <remarks>
/// The session tag allows IAM policies to restrict access by customer, via "aws:PrincipalTag". Without this all
/// requests are made using the ambient credentials for the current task.
/// </remarks>
public class AssumeRoleSettings
{
    /// <summary>
    /// If true, supported AWS clients are created per-customer using an assumed, tagged, role.
    /// If false, clients use the ambient credentials for the current task.
    /// </summary>
    /// <remarks>This is ignored, and treated as false, if LocalStack is in use</remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Arn of role to assume. 
    /// </summary>
    /// <remarks>Role must have a trust policy that allows it be assumed, and perms to tag the session.</remarks>
    public string? RoleArn { get; set; }

    /// <summary>
    /// How long, in seconds, an assumed role session is valid for. The SDK refreshes credentials automatically before
    /// they expire.
    /// </summary>
    /// <remarks>3600 is the maximum when an assumed role is assuming another role</remarks>
    public int DurationSeconds { get; set; } = 3600;

    /// <summary>
    /// Key of session tag that stores the current customer.
    /// </summary>
    public string TagKey { get; set; } = "Customer";

    /// <summary>
    /// Prefix used for the assumed role session name, the customer id is appended to this
    /// </summary>
    public string SessionNamePrefix { get; set; } = "customer-";

    /// <summary>
    /// Maximum number of items held in each cache. Credentials, and each type of client, are cached separately.
    /// </summary>
    /// <remarks>
    /// Cached clients share a single, process-wide, HttpClient so this bound is to avoid unbounded bookkeeping rather
    /// than to limit connections.
    /// </remarks>
    public int MaxCachedClients { get; set; } = 100;

    /// <summary>
    /// How long, in minutes, an unused cache entry is kept for
    /// </summary>
    public int CacheIdleMinutes { get; set; } = 60;
}
