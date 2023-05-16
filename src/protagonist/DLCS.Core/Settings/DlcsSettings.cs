﻿using System;
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
    /// If true, the legacy/Deliverator message format is used for requests to Engine
    /// </summary>
    public bool UseLegacyEngineMessage { get; set; }
    
    public Uri EngineDirectIngestUri { get; set; }
    
    /// <summary>
    /// A collection of default settings for ingesting assets, keyed by AssetFamily
    /// </summary>
    public IngestDefaultSettings IngestDefaults { get; set; }
}

public class IngestDefaultSettings
{
    /// <summary>
    /// A collection of default settings for ingesting assets, keyed by AssetFamily
    /// </summary>
    public Dictionary<string, IngestFamilyDefaults> FamilyDefaults { get; set; }

    /// <summary>
    /// Default storage policy
    /// </summary>
    public string StoragePolicy { get; set; }

    // Naive cache of presets that have already been fetched
    private static readonly Dictionary<string, IngestPresets> CachedPresets = new();
    
    /// <summary>
    /// Get ingest presets for specific family and mediaType
    /// </summary>
    public IngestPresets GetPresets(char family, string mediaType)
    {
        var cacheKey = $"{family}:{mediaType}";
        if (CachedPresets.TryGetValue(cacheKey, out var presets))
        {
            return presets;
        }

        var newPreset = GetPresetInternal(family, mediaType);
        CachedPresets[cacheKey] = newPreset;
        return newPreset;
    }
    
    private IngestPresets GetPresetInternal(char family, string mediaType)
    {
        const string catchAllPolicy = "*";
        if (!FamilyDefaults.TryGetValue(family.ToString(), out var defaultForFamily))
        {
            throw new ArgumentOutOfRangeException(nameof(family), family, "Could not find defaults for provided family");
        }

        string? GetMatchingPolicy(Dictionary<string, string>? policyDict)
        {
            if (policyDict.IsNullOrEmpty()) return null;

            var matchingPolicy = policyDict.FirstOrDefault(p => mediaType.StartsWith(p.Key)).Value;
            return matchingPolicy.HasText()
                ? matchingPolicy
                : policyDict.SingleOrDefault(a => a.Key == catchAllPolicy).Value;
        }

        var optimisationPolicy = GetMatchingPolicy(defaultForFamily.OptimisationPolicy);
        var thumbnailPolicy = GetMatchingPolicy(defaultForFamily.ThumbnailPolicy);

        return new IngestPresets(optimisationPolicy, defaultForFamily.DeliveryChannel, thumbnailPolicy);
    }
}

public class IngestFamilyDefaults
{
    /// <summary>
    /// A collection of optimisation policies, keyed by mediaType. e.g. "audio"/"video".
    /// Key is "*" for all in that family, or as a default if there are no other matching items.
    /// </summary>
    public Dictionary<string, string> OptimisationPolicy { get; set; }
    
    /// <summary>
    /// Default delivery-channel for family
    /// </summary>
    public string DeliveryChannel { get; set; }
    
    /// <summary>
    /// A collection of thumbnail policies, keyed by mediaType. e.g. "audio"/"video".
    /// Key is "*" for all in that family, or as a default if there are no other matching items.
    /// </summary>
    public Dictionary<string, string> ThumbnailPolicy { get; set; }
}