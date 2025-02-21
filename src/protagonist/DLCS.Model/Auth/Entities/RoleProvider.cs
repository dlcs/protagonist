#nullable disable

namespace DLCS.Model.Auth.Entities;

/// <summary>
/// Configuration of how to obtain Role information for a given auth service
/// </summary>
public class RoleProvider
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public string AuthService { get; set; }
    
    /// <summary>
    /// Base64 encoded string containing RoleProviderConfiguration
    /// </summary>
    public string Configuration { get; set; }

    /// <summary>
    /// Optional credentials for accessing role-provider
    /// </summary>
    public string Credentials { get; set; }
}
