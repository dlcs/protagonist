using System;
using API.Features.DeliveryChannelPolicies.Validation;
using DLCS.HydraModel;
using FluentValidation.TestHelper;

namespace API.Tests.Features.DeliveryChannelPolicies.Validation;

public class HydraDeliveryChannelPolicyValidatorTests
{
    private readonly HydraDeliveryChannelPolicyValidator sut;
    
    public HydraDeliveryChannelPolicyValidatorTests()
    {
        sut = new HydraDeliveryChannelPolicyValidator();
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
    public void NewDeliveryChannelPolicy_CannotHave_Channel()
    {
        var policy = new DeliveryChannelPolicy()
        {
            Channel = "iif-img"
        };
        var result = sut.TestValidate(policy);
        result.ShouldHaveValidationErrorFor(p => p.Channel);
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
    public void NewDeliveryChannelPolicy_CannotHave_Name_OnPutOrPatch()
    {
        var policy = new DeliveryChannelPolicy()
        {
            Name = "my-delivery-channel-policy"
        };
        var result = sut.TestValidate(policy, p => p.IncludeRuleSets("default", "put-patch"));
        result.ShouldHaveValidationErrorFor(p => p.Name);
    }
    
    [Fact]
    public void NewDeliveryChannelPolicy_Requires_PolicyData_OnPutOrPatch()
    {
        var policy = new DeliveryChannelPolicy()
        {
            PolicyData = null,
        };
        var result = sut.TestValidate(policy, p => p.IncludeRuleSets("default", "put-patch"));
        result.ShouldHaveValidationErrorFor(p => p.PolicyData);
    }
}