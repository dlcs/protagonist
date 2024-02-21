using System.Text.Json;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF.ImageApi;

namespace API.Features.DeliveryChannelPolicies.Validation;

public class DeliveryChannelPolicyDataValidator
{
    public bool Validate(string policyDataJson, string channel)
    {
        return channel switch
        {
            AssetDeliveryChannels.Thumbnails => ValidateThumbnailPolicyData(policyDataJson),
            AssetDeliveryChannels.Timebased => ValidateTimeBasedPolicyData(policyDataJson),
            _ => false // This is only for thumbs and iiif-av for now
        };
    }
    
    private string[]? ParseJsonPolicyData(string policyDataJson)
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
    
    private bool ValidateThumbnailPolicyData(string policyDataJson)
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

    private bool ValidateTimeBasedPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);
        
        // For now, we only expect a single string value
        if (policyData == null || policyData.Length != 1 || string.IsNullOrEmpty((policyData[0]))) 
        {
            return false;
        }

        return true;
    }
}