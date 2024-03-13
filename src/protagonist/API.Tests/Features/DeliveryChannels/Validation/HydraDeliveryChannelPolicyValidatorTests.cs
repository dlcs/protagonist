using System;
using API.Features.DeliveryChannels.Validation;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using DLCS.Model.DeliveryChannels;
using FakeItEasy;
using FluentValidation.TestHelper;

namespace API.Tests.Features.DeliveryChannelPolicies.Validation;

public class HydraDeliveryChannelPolicyValidatorTests
{
    private readonly HydraDeliveryChannelPolicyValidator sut;
    private readonly string[] fakedAvPolicies =
    {
        "video-mp4-480p",
        "video-webm-720p",
        "audio-mp3-128k"
    };
    
    public HydraDeliveryChannelPolicyValidatorTests()
    {
        var avChannelPolicyOptionsRepository = A.Fake<IAvChannelPolicyOptionsRepository>();
        A.CallTo(() => avChannelPolicyOptionsRepository.RetrieveAvChannelPolicyOptions())
            .Returns(fakedAvPolicies);
        sut = new HydraDeliveryChannelPolicyValidator(
            new DeliveryChannelPolicyDataValidator(avChannelPolicyOptionsRepository));
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_CannotHave_AssetId()
    {
        var policy = new DeliveryChannelPolicy()
        {
            Id = "foo",
        };
        var result = sut.TestValidate(policy);
        result.ShouldHaveValidationErrorFor(p => p.Id);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_CannotHave_CustomerId()
    {
        var policy = new DeliveryChannelPolicy()
        {
            CustomerId = 1,
        };
        var result = sut.TestValidate(policy);
        result.ShouldHaveValidationErrorFor(p => p.CustomerId);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_CannotHave_PolicyCreated()
    {
        var policy = new DeliveryChannelPolicy()
        {  
            Created = DateTime.UtcNow
        };
        var result = sut.TestValidate(policy);
        result.ShouldHaveValidationErrorFor(p => p.Created);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_CannotHave_PolicyModified()
    {
        var policy = new DeliveryChannelPolicy()
        {  
            Modified = DateTime.UtcNow
        };
        var result = sut.TestValidate(policy);
        result.ShouldHaveValidationErrorFor(p => p.Modified);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_Requires_Name_OnPost()
    {
        var policy = new DeliveryChannelPolicy()
        {
            Name = null
        };
        var result = sut.TestValidate(policy, p => p.IncludeRuleSets("default", "post"));
        result.ShouldHaveValidationErrorFor(p => p.Name);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_Requires_PolicyData_OnPost()
    {
        var policy = new DeliveryChannelPolicy()
        {
            PolicyData = null,
        };
        var result = sut.TestValidate(policy, p => p.IncludeRuleSets("default", "post"));
        result.ShouldHaveValidationErrorFor(p => p.PolicyData);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_Requires_PolicyData_OnPut()
    {
        var policy = new DeliveryChannelPolicy()
        {
            PolicyData = null,
        };
        var result = sut.TestValidate(policy, p => p.IncludeRuleSets("default", "put"));
        result.ShouldHaveValidationErrorFor(p => p.PolicyData);
    }
    
    [Fact]
    public async void NewDeliveryChannelPolicy_Requires_ValidTranscodePolicy_ForAvChannel()
    {
        var policy = new DeliveryChannelPolicy()
        {
            Channel = AssetDeliveryChannels.Timebased,
            PolicyData = "[\"not-a-transcode-policy\"]"
        };
        var result = await sut.TestValidateAsync(policy);
        result.ShouldHaveValidationErrorFor(p => p.PolicyData);
    }
    
    [Theory]
    [InlineData("[\"video-mp4-480p\"]")]
    [InlineData("[\"video-webm-720p\"]")]
    [InlineData("[\"audio-mp3-128k\"]")]
    public async void NewDeliveryChannelPolicy_Accepts_ValidTranscodePolicy_ForAvChannel(string policyData)
    {
        var policy = new DeliveryChannelPolicy()
        {
            Channel = AssetDeliveryChannels.Timebased,
            PolicyData = policyData
        };
        var result = await sut.TestValidateAsync(policy);
        result.ShouldNotHaveValidationErrorFor(p => p.PolicyData);
    }
}