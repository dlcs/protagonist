using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Messaging;
using Newtonsoft.Json;

namespace DLCS.Repository.Messaging;

/// <summary> 
/// This is for temporary compatibility with legacy Engine, we need to send this request body to engine in
/// the format legacy engine expects. This signal doesn't have to look like this though in new Protagonist.
/// </summary>
internal static class LegacyJsonMessageHelpers
{
    public static async Task<string> GetLegacyJsonString(IngestAssetRequest ingestAssetRequest, bool derivativesOnly)
    {
        var stringParams = new Dictionary<string, string>
        {
            ["id"] = ingestAssetRequest.Asset.Id,
            ["customer"] = ingestAssetRequest.Asset.Customer.ToString(),
            ["space"] = ingestAssetRequest.Asset.Space.ToString(),
            ["image"] = AsJsonStringForMessaging(ingestAssetRequest.Asset)
        };

        // we'll never set initialorigin in our limited first port of this
        if (derivativesOnly)
        {
            stringParams["operation"] = "derivatives-only";
        }

        await using var stringWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(stringWriter))
        {
            ToLegacyMessageJson(jsonWriter, "event::image-ingest", stringParams);
        }

        var jsonString = stringWriter.ToString();
        return jsonString;
    }
    
    private static string AsJsonStringForMessaging(Asset asset)
    {
        using var stringWriter = new StringWriter();
        using (var jsonWriter = new JsonTextWriter(stringWriter))
        {
            jsonWriter.WriteStartObject();
            WriteJsonProperties(jsonWriter, asset);
            jsonWriter.WriteEndObject();
        }
        return stringWriter.ToString();
    }
    
    private static void ToLegacyMessageJson(JsonWriter json, string message, Dictionary<string, string> stringParams)
    {
        // This replicates the payload created by the Inversion MessagingEvent class.
        json.WriteStartObject();
        json.WritePropertyName("_type");
        json.WriteValue("event");
        json.WritePropertyName("_created");
        json.WriteValue(DateTime.UtcNow.ToString("o"));
        json.WritePropertyName("message");
        json.WriteValue(message);
        json.WritePropertyName("params");
        json.WriteStartObject();
        foreach (KeyValuePair<string, string> keyValuePair in (IEnumerable<KeyValuePair<string, string>>) stringParams)
        {
            json.WritePropertyName(keyValuePair.Key);
            json.WriteValue(keyValuePair.Value);
        }
        json.WriteEndObject();
        json.WriteEndObject();
    }

    static void WriteJsonProperties(JsonWriter writer, Asset asset)
    {
        bool asLinkedData = false;
        
        // what follows is copied exactly from deliverator.
        // We do not need to preserve this message format but obvs we need to change this and Engine together.
        if (!asLinkedData)
        {
            writer.WritePropertyName("id");
            writer.WriteValue(asset.Id);
            writer.WritePropertyName("customer");
            writer.WriteValue(asset.Customer);
            writer.WritePropertyName("space");
            writer.WriteValue(asset.Space);
            writer.WritePropertyName("rawId");
            writer.WriteValue(asset.GetUniqueName());
        }
        
        writer.WritePropertyName("created");
        writer.WriteValue(asset.Created);
        writer.WritePropertyName("origin");
        writer.WriteValue(asset.Origin);
        writer.WritePropertyName("tags");
        writer.WriteStartArray();
        foreach (string tag in asset.TagsList)
        {
            writer.WriteValue(tag);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("roles");
        writer.WriteStartArray();
        foreach (string role in asset.RolesList)
        {
            if (!asLinkedData)
            {
                writer.WriteValue(role);
            }
            else if (role.ToLowerInvariant().StartsWith("http"))
            {
                writer.WriteValue(role);
            }
            else
            {
                // ignore this for now
                // writer.WriteValue(String.Format("{0}/customers/{1}/roles/{2}", Context.BaseURL, this.Customer, role));
            }
        }
        writer.WriteEndArray();
        writer.WritePropertyName("preservedUri");
        writer.WriteValue(asset.PreservedUri);
        writer.WritePropertyName("string1");
        writer.WriteValue(asset.Reference1);
        writer.WritePropertyName("string2");
        writer.WriteValue(asset.Reference2);
        writer.WritePropertyName("string3");
        writer.WriteValue(asset.Reference3);
        writer.WritePropertyName("maxUnauthorised");
        writer.WriteValue(asset.MaxUnauthorised);
        writer.WritePropertyName("number1");
        writer.WriteValue(asset.NumberReference1);
        writer.WritePropertyName("number2");
        writer.WriteValue(asset.NumberReference2);
        writer.WritePropertyName("number3");
        writer.WriteValue(asset.NumberReference3);
        writer.WritePropertyName("width");
        writer.WriteValue(asset.Width);
        writer.WritePropertyName("height");
        writer.WriteValue(asset.Height);
        writer.WritePropertyName("duration");
        writer.WriteValue(asset.Duration);
        writer.WritePropertyName("error");
        writer.WriteValue(asset.Error);
        writer.WritePropertyName("batch");
        writer.WriteValue(asset.Batch);

        writer.WritePropertyName("finished");
        if (asset.Finished == DateTime.MinValue)
        {
            writer.WriteNull();
        }
        else
        {
            writer.WriteValue(asset.Finished);
        }
        
        writer.WritePropertyName("ingesting");
        writer.WriteValue(asset.Ingesting);

        writer.WritePropertyName("imageOptimisationPolicy");
        if (!asLinkedData)
        {
            writer.WriteValue(asset.ImageOptimisationPolicy);
        }
        else
        {
            // writer.WriteValue(String.Format("{0}/imageOptimisationPolicies/{1}",
            //     Context.BaseURL,
            //     this.ImageOptimisationPolicy));
        }

        writer.WritePropertyName("thumbnailPolicy");
        if (!asLinkedData)
        {
            writer.WriteValue(asset.ThumbnailPolicy);
        }
        else
        {
            // writer.WriteValue(String.Format("{0}/thumbnailPolicies/{1}", Context.BaseURL, this.ThumbnailPolicy));
        }

        if (!String.IsNullOrEmpty(asset.InitialOrigin))
        {
            writer.WritePropertyName("initialOrigin");
            writer.WriteValue(asset.InitialOrigin);
        }

        writer.WritePropertyName("family");
        writer.WriteValue((char)(asset.Family ?? AssetFamily.Image));

        writer.WritePropertyName("mediaType");
        writer.WriteValue(asset.MediaType);

        if (asLinkedData)
        {
            // writer.WritePropertyName("storage");
            // writer.WriteValue(String.Format("{0}/customers/{1}/spaces/{2}/images/{3}/storage", Context.BaseURL,
            //     this.Customer, this.Space, this.GetUniqueName()));
            //
            // writer.WritePropertyName("metadata");
            // writer.WriteValue(String.Format("{0}/customers/{1}/spaces/{2}/images/{3}/metadata",
            //     Context.BaseURL, this.Customer, this.Space, this.GetUniqueName()));
        }
    }
}