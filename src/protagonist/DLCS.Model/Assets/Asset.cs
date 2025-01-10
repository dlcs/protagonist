using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets.Metadata;

namespace DLCS.Model.Assets;

/// <summary>
/// Represents an Asset that is stored in the DLCS database.
/// </summary>
public class Asset : ICloneable
{
    public AssetId Id { get; set; }
    public int Customer { get; set; }
    public int Space { get; set; }
    public DateTime? Created { get; set; }
    public string? Origin { get; set; }
    public string? Tags { get; set; }
    public string? Roles { get; set; }
    public string? PreservedUri { get; set; }
    public string? Reference1 { get; set; }
    public string? Reference2 { get; set; }
    public string? Reference3 { get; set; }
    public int? NumberReference1 { get; set; }
    public int? NumberReference2 { get; set; }
    public int? NumberReference3 { get; set; }
    
    /// <summary>
    /// The maximum size of longest dimension that is viewable by unauthorised users.
    /// -1 = null (all open), 0 = no allowed size without being auth 
    /// </summary>
    public int? MaxUnauthorised { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Error { get; set; }
    public int? Batch { get; set; }
    public DateTime? Finished { get; set; }
    public bool? Ingesting { get; set; }
    public string? ImageOptimisationPolicy { get; set; }
    public string? ThumbnailPolicy { get; set; }
    public AssetFamily? Family { get; set; }
    public string? MediaType { get; set; }
    public long? Duration { get; set; }
    
    /// <summary>
    /// Flags the asset as not to be delivered for viewing under any circumstances
    /// </summary>
    public bool NotForDelivery { get; set; }

    /// <summary>
    /// A list of 1:n delivery channels for asset. Dictates which asset-delivery channels are available
    /// </summary>
    public string[] DeliveryChannels { get; set; } = Array.Empty<string>();

    private IEnumerable<string>? rolesList;
    
    // TODO - map this via Dapper on way out of DB?
    [NotMapped]
    public IEnumerable<string> RolesList
    {
        get
        {
            if (rolesList == null && !string.IsNullOrEmpty(Roles))
            {
                rolesList = Roles.Split(",", StringSplitOptions.RemoveEmptyEntries); 
            }

            return rolesList ??= Enumerable.Empty<string>();
        }
        set => Roles = value.IsNullOrEmpty() ? String.Empty : String.Join(',', value);
    }
    
    private IEnumerable<string>? tagsList;
    
    [NotMapped]
    public IEnumerable<string> TagsList
    {
        get
        {
            if (tagsList == null && !string.IsNullOrEmpty(Tags))
            {
                tagsList = Tags.Split(",", StringSplitOptions.RemoveEmptyEntries); 
            }

            return tagsList ??= Enumerable.Empty<string>();
        }
        set => Tags = value.IsNullOrEmpty() ? String.Empty : String.Join(',', value);
    }
    
    /// <summary>
    /// Indicates whether this asset requires authentication to view. This is required if either Roles are assigned
    /// OR MaxUnauthorised >= 0
    /// </summary>
    public bool RequiresAuth => !string.IsNullOrWhiteSpace(Roles) || MaxUnauthorised >= 0;

    /// <summary>
    /// A list of image delivery channels attached to this asset
    /// </summary>
    public ICollection<ImageDeliveryChannel> ImageDeliveryChannels { get; set; }
    
    /// <summary>
    /// A list of metadata attached to this asset
    /// </summary>
    public ICollection<AssetApplicationMetadata>? AssetApplicationMetadata { get; set; }
    
    /// <summary>
    /// A list of batch assets attached to this asset
    /// </summary>
    public List<BatchAsset>? BatchAssets { get; set; }

    public Asset()
    {
    }

    public Asset(AssetId assetId)
    {
        Id = assetId;
        Customer = assetId.Customer;
        Space = assetId.Space;
    }

    public Asset Clone()
    {
        var asset = (Asset)MemberwiseClone();

        var deliveryChannels = asset.ImageDeliveryChannels.Select(d => (ImageDeliveryChannel)d.Clone()).ToList();
        asset.ImageDeliveryChannels = deliveryChannels;
        
        if (asset.AssetApplicationMetadata != null)
        {
            var assetApplicationMetadata =
                asset.AssetApplicationMetadata.Select(a => (AssetApplicationMetadata)a.Clone()).ToList();
            asset.AssetApplicationMetadata = assetApplicationMetadata;
        }

        return asset;
    }
    
    object ICloneable.Clone() { return Clone(); }
}