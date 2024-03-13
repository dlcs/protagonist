using System.Text.Json;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using IIIF.ImageApi;

namespace API.Features.DeliveryChannels.Validation;

public class DeliveryChannelPolicyDataValidator
{
    private readonly IAvPolicyOptionsRepository avPolicyOptionsRepository;

    public DeliveryChannelPolicyDataValidator(IAvPolicyOptionsRepository avPolicyOptionsRepository)
    {
        this.avPolicyOptionsRepository = avPolicyOptionsRepository;
    }

    public async Task<bool> Validate(string policyDataJson, string channel)
    {
        return channel switch
        {
            AssetDeliveryChannels.Thumbnails => ValidateThumbnailPolicyData(policyDataJson),
            AssetDeliveryChannels.Timebased => await ValidateTimeBasedPolicyData(policyDataJson),
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
        catch(JsonException)
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

    private async Task<bool> ValidateTimeBasedPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);
        
        if (policyData.IsNullOrEmpty() || policyData.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        var avChannelPolicyOptions = 
            await avPolicyOptionsRepository.RetrieveAvChannelPolicyOptions();

        return policyData.All(avPolicy => avChannelPolicyOptions.Contains(avPolicy));
    }
}