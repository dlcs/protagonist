using System.Text.Json;
using API.Exceptions;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
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
        catch(JsonException)
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
                if (!IsValidThumbnailParameter(SizeParameter.Parse(sizeValue)))
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

    private bool IsValidThumbnailParameter(SizeParameter param) => param switch
        {
            { Max: true } => false,
            { PercentScale: not null } => false,
            { Confined: false, Width: not null, Height: not null } => false,
            { Confined: true } and ({ Width: null } or { Height : null }) => false,
            { Width: null, Height: null } => false,
            _ => true,
        };

    private async Task<bool> ValidateTimeBasedPolicyData(string policyDataJson)
    {
        var policyData = ParseJsonPolicyData(policyDataJson);
        
        if (policyData.IsNullOrEmpty() || policyData.Any(string.IsNullOrEmpty))
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