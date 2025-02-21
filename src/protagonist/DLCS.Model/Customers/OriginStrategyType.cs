using System.ComponentModel;

namespace DLCS.Model.Customers;

/// <summary>
/// Represents a type of CustomerOriginStrategy which determines how resources are fetched.
/// </summary>
public enum OriginStrategyType
{
    /// <summary>
    /// Use unauthorised http request to fetch original source. Default origin strategy. 
    /// </summary>
    [Description("default")]
    Default = 0,
    
    /// <summary>
    /// Use basic-authentication headers for http request to fetch original source.
    /// </summary>
    [Description("basic-http-authentication")]
    BasicHttp = 1,
    
    /// <summary>
    /// Use ambient S3 credentials to fetch resource via s3 cli.
    /// </summary>
    [Description("s3-ambient")]
    S3Ambient = 2,
    
    /// <summary>
    /// Use credentials to fetch origin via sftp.
    /// </summary>
    [Description("sftp")]
    SFTP = 3
}