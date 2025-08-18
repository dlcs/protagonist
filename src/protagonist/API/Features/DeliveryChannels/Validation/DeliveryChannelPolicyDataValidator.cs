using System.Text.Json;
using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using DLCS.Model.IIIF;
using IIIF.ImageApi;

namespace API.Features.DeliveryChannels.Validation;

public class DeliveryChannelPolicyDataValidator
{
    private readonly IAvChannelPolicyOptionsRepository avChannelPolicyOptionsRepository;

    public DeliveryChannelPolicyDataValidator(IAvChannelPolicyOptionsRepository avChannelPolicyOptionsRepository)
    {
        this.avChannelPolicyOptionsRepository = avChannelPolicyOptionsRepository;
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
        catch (JsonException)
        {
            return Array.Empty<string>();
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
                if (!SizeParameter.Parse(sizeValue).IsValidThumbnailParameter())
                {
                    return false;
                }
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

        // Invalid if no data, contains empty values or duplicate values
        if (policyData.IsNullOrEmpty() || policyData.Any(string.IsNullOrEmpty) ||
            policyData.Distinct().Count() != policyData.Length)
        {
            return false;
        }

        var avChannelPolicyOptions = 
            await avChannelPolicyOptionsRepository.RetrieveAvChannelPolicyOptions();

        if (avChannelPolicyOptions == null)
        {
            throw new APIException("Unable to retrieve available iiif-av policies from engine");
        }
        
        return policyData.All(avPolicy => avChannelPolicyOptions.Contains(avPolicy));
    }
}
