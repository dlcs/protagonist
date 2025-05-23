﻿using System;

namespace DLCS.Core.Settings;

public class DlcsSettings
{
    /// <summary>
    /// The base URI of DLCS to hand-off requests to.
    /// </summary>
    public Uri ApiRoot { get; set; }
    
    /// <summary>
    /// The base URI for image services and other public-facing resources
    /// </summary>
    public Uri ResourceRoot { get; set; }
    
    /// <summary>
    /// The base URI for the engine
    /// </summary>
    public Uri EngineRoot { get; set; } 

    /// <summary>
    /// Default timeout for dlcs api requests.
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// URL format of NamedQuery for generating manifest for space.
    /// </summary>
    public string SpaceManifestQuery { get; set; }
    
    /// <summary>
    /// URL format for generating manifests for single assets
    /// </summary>
    public string SingleAssetManifestTemplate { get; set; }
    
    /// <summary>
    /// 256bit or longer, Base64 encoded, JWT secret
    /// </summary>
    public string? JwtKey { get; set; }

    /// <summary>
    /// List of valid issuers of JWT for authentication
    /// </summary>
    public string[] JwtValidIssuers { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Max number of stored items for "default" storage policy 
    /// </summary>
    /// <remarks>These are used during 1 off setup only</remarks>
    public long DefaultPolicyMaxNumber { get; set; } = 1000000000;
    
    /// <summary>
    /// Max number of stored bytes for "default" storage policy
    /// </summary>
    /// <remarks>These are used during 1 off setup only</remarks>
    public long DefaultPolicyMaxSize { get; set; } = 1000000000000000;
}
