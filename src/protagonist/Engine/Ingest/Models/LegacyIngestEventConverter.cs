using System.Text.Json;
using System.Text.Json.Nodes;
using DLCS.Core.Guard;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;

namespace Engine.Ingest.Models;

public static class LegacyIngestEventConverter
{
    /// <summary>
    /// Convert to IngestAssetRequest object.
    /// </summary>
    /// <param name="incomingRequest">Event to convert</param>
    /// <returns>IngestAssetRequest</returns>
    /// <exception cref="InvalidOperationException">Thrown if IncomingIngestEvent doesn't contain any Asset data</exception>
    public static IngestAssetRequest ConvertToAssetRequest(this LegacyIngestEvent incomingRequest)
    {
        incomingRequest.ThrowIfNull(nameof(incomingRequest));

        if (string.IsNullOrEmpty(incomingRequest.AssetJson))
        {
            throw new InvalidOperationException("Cannot convert LegacyIngestEvent that has no Asset Json");
        }

        try
        {
            var formattedJson = incomingRequest.AssetJson.Replace("\r\n", string.Empty);
            var asset = ConvertJsonToAsset(formattedJson);
            return new IngestAssetRequest(asset.Id, incomingRequest.Created);
        }
        catch (JsonException e)
        {
            var ex = new InvalidOperationException("Unable to deserialize Asset Json from LegacyIngestEvent", e);
            ex.Data.Add("AssetJson", incomingRequest.AssetJson);
            throw ex;
        }
    }

    // This is very temporary and should be removed asap, only included for backwards compat
    private static Asset ConvertJsonToAsset(string assetJsonString)
    {
        var parsedJson = JsonObject.Parse(assetJsonString).AsObject();

        var asset = new Asset();
        asset.Id = parsedJson.TryGetPropertyValue("id", out var id) ? AssetId.FromString(id.GetValue<string>()) : null;
        asset.Customer = parsedJson.TryGetPropertyValue("customer", out var customer) ? customer.GetValue<int>() : 0;
        asset.Space = parsedJson.TryGetPropertyValue("space", out var space) ? space.GetValue<int>() : 0;
        asset.Created = parsedJson.TryGetPropertyValue("created", out var created) ? created.GetValue<DateTime>() : null;
        asset.Origin = parsedJson.TryGetPropertyValue("origin", out var origin) ? origin.GetValue<string>() : null;
        asset.Reference1 = parsedJson.TryGetPropertyValue("string1", out var string1) ? string1.GetValue<string>() : null;
        asset.Reference2 = parsedJson.TryGetPropertyValue("string2", out var string2) ? string2.GetValue<string>() : null;
        asset.Reference3 = parsedJson.TryGetPropertyValue("string3", out var string3) ? string3.GetValue<string>() : null;
        asset.PreservedUri = parsedJson.TryGetPropertyValue("preservedUri", out var preservedUri)
            ? preservedUri.GetValue<string>()
            : null;
        asset.MaxUnauthorised = parsedJson.TryGetPropertyValue("maxUnauthorised", out var maxUnauthorised)
            ? maxUnauthorised.GetValue<int>()
            : 0;
        asset.NumberReference1 = parsedJson.TryGetPropertyValue("number1", out var number1) ? number1.GetValue<int>() : 0;
        asset.NumberReference2 = parsedJson.TryGetPropertyValue("number2", out var number2) ? number2.GetValue<int>() : 0;
        asset.NumberReference3 = parsedJson.TryGetPropertyValue("number3", out var number3) ? number3.GetValue<int>() : 0;
        asset.Width = parsedJson.TryGetPropertyValue("width", out var width) ? width.GetValue<int>() : 0;
        asset.Height = parsedJson.TryGetPropertyValue("height", out var height) ? height.GetValue<int>() : 0;
        asset.Duration = parsedJson.TryGetPropertyValue("duration", out var duration) ? duration.GetValue<long>() : 0;
        asset.Error = parsedJson.TryGetPropertyValue("error", out var error) ? error.GetValue<string>() : null;
        asset.Batch = parsedJson.TryGetPropertyValue("batch", out var batch) ? batch.GetValue<int>() : 0;
        asset.Finished = parsedJson.TryGetPropertyValue("finished", out var finished) && finished != null
            ? finished.GetValue<DateTime?>()
            : null;
        asset.Ingesting = parsedJson.TryGetPropertyValue("ingesting", out var ingesting)
            ? ingesting.GetValue<bool>()
            : null;
        asset.MediaType = parsedJson.TryGetPropertyValue("mediaType", out var mediaType)
            ? mediaType.GetValue<string>()
            : null;
        asset.TagsList = parsedJson.TryGetPropertyValue("tags", out var tags) ? tags.Deserialize<string[]>() : null;
        asset.RolesList = parsedJson.TryGetPropertyValue("roles", out var roles) ? roles.Deserialize<string[]>() : null;
        asset.ImageOptimisationPolicy = parsedJson.TryGetPropertyValue("imageOptimisationPolicy", out var imageOptimisationPolicy)
            ? imageOptimisationPolicy.GetValue<string>()
            : null;
        asset.ThumbnailPolicy = parsedJson.TryGetPropertyValue("thumbnailPolicy", out var thumbnailPolicy)
            ? thumbnailPolicy.GetValue<string>()
            : null;

        if (parsedJson.TryGetPropertyValue("family", out var family))
        {
            var familyChar = family.GetValue<char>();
            asset.Family = (AssetFamily)familyChar;
        }
        else
        {
            asset.Family = AssetFamily.Image;
        }

        return asset;
    }
}