using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Strings;

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
    /// If true, the legacy/Deliverator message format is used for requests to Engine
    /// </summary>
    public bool UseLegacyEngineMessage { get; set; }
    
    public Uri EngineDirectIngestUri { get; set; }
}