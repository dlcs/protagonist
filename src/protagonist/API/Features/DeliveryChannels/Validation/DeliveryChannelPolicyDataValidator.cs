using System.Text.Json;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using IIIF.ImageApi;

namespace API.Features.DeliveryChannels.Validation;

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

        foreach (var sizeValue in policyData)
        {
            try
            {
                SizeParameter.Parse(sizeValue);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateTimeBasedPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);

        return !(policyData.IsNullOrEmpty() || policyData.Any(string.IsNullOrEmpty));
    }
}