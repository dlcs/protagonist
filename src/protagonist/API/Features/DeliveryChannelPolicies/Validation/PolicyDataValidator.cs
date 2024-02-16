using System.Text.Json;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF.ImageApi;

namespace API.Features.DeliveryChannelPolicies.Validation;

public static class PolicyDataValidator
{
    private static string[] validTimeBasedFormats =
    {
        "video-mp4-720p", 
        "audio-mp3-128"
    };
    
    public static bool Validate(string policyDataJson, string channel)
    {
        return channel switch
        {
            AssetDeliveryChannels.Thumbnails => ValidateThumbnailPolicyData(policyDataJson),
            AssetDeliveryChannels.Timebased => ValidateTimeBasedPolicyData(policyDataJson),
            _ => false // This is only for thumbs and iiif-av for now
        };
    }
    
    private static string[]? ParseJsonPolicyData(string policyDataJson)
    {
        string[]? policyData;
        try
        {
            policyData = JsonSerializer.Deserialize<string[]>(policyDataJson);
        }
        catch(JsonException ex)
        {
            return null;
        }

        return policyData;
    }
    
    private static bool ValidateThumbnailPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);
        
        if (policyData.IsNullOrEmpty())
        {
            return false;
        }
        
        var isInvalid = false;
        
        foreach (var sizeValue in policyData)
        {
            try { SizeParameter.Parse(sizeValue); }
            catch
            {
                isInvalid = true;
                break;
            }
        }
        
        return !isInvalid;
    }

    private static bool ValidateTimeBasedPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);
        
        if (policyData.IsNullOrEmpty())
        {
            return false;
        }
        
        return validTimeBasedFormats.Contains(policyData[0]);
    }
}