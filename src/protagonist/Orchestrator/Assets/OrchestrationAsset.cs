﻿using System;
using System.Collections.Generic;
using DLCS.Core.Types;
using Microsoft.Extensions.Primitives;

namespace Orchestrator.Assets;

/// <summary>
/// Represents an asset during orchestration.
/// </summary>
public class OrchestrationAsset
{
    /// <summary>
    /// Get or set the AssetId for tracked Asset
    /// </summary>
    public AssetId AssetId { get; set; }

    /// <summary>
    /// Get boolean indicating whether asset is restricted or not.
    /// </summary>
    public bool RequiresAuth { get; set; }

    /// <summary>
    /// Gets list of roles associated with Asset
    /// </summary>
    public List<string> Roles { get; set; } = new();
    
    /// <summary>
    /// Get or set Asset origin 
    /// </summary>
    /// <remarks>This is currently only used when "File" channel is available</remarks>
    public string? Origin { get; set; }
    
    /// <summary>
    /// Get or set whether this asset has an optimised origin 
    /// </summary>
    /// <remarks>This is currently only used when "File" channel is available</remarks>
    public bool? OptimisedOrigin { get; set; }

    /// <summary>
    /// A list of which delivery channels this asset is available on
    /// </summary>
    public AvailableDeliveryChannel Channels { get; set; } = AvailableDeliveryChannel.NotSet;
    
    /// <summary>
    /// Get or set the asset media-type
    /// </summary>
    public StringValues? MediaType { get; set; }
}

public class OrchestrationImage : OrchestrationAsset
{
    /// <summary>
    /// Get or set asset Width
    /// </summary>
    public int Width { get; set; }
    
    /// <summary>
    /// Get or set asset Height
    /// </summary>
    public int Height { get; set; }
    
    /// <summary>
    /// Get maximum dimension available for unauthorised user
    /// </summary>
    public int MaxUnauthorised { get; set; }

    /// <summary>
    /// Gets or sets list of thumbnail sizes
    /// </summary>
    public List<int[]> OpenThumbs { get; set; } = new();
    
    /// <summary>
    /// Get or set location in S3 where image-server source is located 
    /// </summary>
    public string? S3Location { get; set; }
    
    /// <summary>
    /// Does this image need to be reingested on the fly?
    /// </summary>
    /// <remarks>This can only be true for legacy images with missing ImageLocation value</remarks>
    public bool Reingest { get; set; }

    /// <summary>
    /// Get value indicating that this item should return a 404 as it has never been successfully processed
    /// </summary>
    /// <remarks>If the item has no S3Location AND it won't be reingested on the fly then it is 404</remarks>
    public bool IsNotFound() => string.IsNullOrEmpty(S3Location) && !Reingest;
}

[Flags]
public enum AvailableDeliveryChannel
{
    NotSet = 0,
    None = 1,
    File = 2,
    Image = 4,
    Timebased = 8
}