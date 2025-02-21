using System;

namespace DLCS.Model.Auth;

/// <summary>
/// Configuration properties for role-provider.
/// </summary>
public class RoleProviderConfiguration
{
    /// <summary>
    /// Type of config, e.g. "cas"
    /// </summary>
    public string Config { get; set; }
    
    /// <summary>
    /// Uri for logging in to obtain role session
    /// </summary>
    public Uri Target { get; set; }
    
    /// <summary>
    /// Uri for querying to get list of roles for token
    /// </summary>
    public Uri Roles { get; set; }
    
    /// <summary>
    /// Uri for logging out of session
    /// </summary>
    public Uri Logout { get; set; }
}