using System;
using System.Collections.Generic;
using System.Linq;
using DLCS.HydraModel;

namespace DLCS.Mock.ApiApp;

public static class MockHelp
{
    public static Customer GetByName(this List<Customer> customers, string name)
    {
        return customers.Single(c => c.Name == name);
    }
    public static AuthService GetByIdPart(this List<AuthService> authServices, string idPart)
    {
        return authServices.Single(a => a.ModelId == idPart);
    }
    public static Role GetByCustAndId(this List<Role> roles, int customerId, string idPart)
    {
        return roles.Single(r => r.CustomerId == customerId && r.ModelId == idPart);
    }
    
    public static AuthService MakeAuthService(
        string baseUrl, int customerId, string serviceId, string name, string profile, int ttl,
        string label, string description, string pageLabel, string pageDescription, string callToAction)
    {
        return new AuthService(baseUrl, customerId, serviceId)
        {
            Name = name,
            Profile = profile,
            TimeToLive = ttl,
            Label = label,
            Description = description,
            PageLabel = pageLabel,
            PageDescription = pageDescription,
            CallToAction = callToAction
        };
    }

    public static CustomerOriginStrategy MakeCustomerOriginStrategy(
        string baseUrl, int customerId, int strategyId,
        string regex, string credentials, string originStrategy)
    {
        return new CustomerOriginStrategy(baseUrl, customerId, strategyId)
        {
            Regex = regex, 
            Credentials = credentials, 
            OriginStrategy = originStrategy
        };
    }
    

    public static Image MakeImage(string baseUrl, int customerId, int space, string modelId, 
        DateTime created, string? origin, string? initialOrigin,
        int? width, int? height, int? maxUnauthorised,
        DateTime? queued, DateTime? dequeued, DateTime? finished, bool ingesting, string error,
        string[]? tags, string? string1, string? string2, string? string3,
        int? number1, int? number2, int? number3,
        string imageOptimisationPolicy, string thumbnailPolicy)
    {
        var image = new Image(baseUrl, customerId, space, modelId);
        string mockDlcsPathTemplate = string.Format("/{0}/{1}/{2}", customerId, space, modelId);
        image.InfoJson = "https://mock.dlcs.io" + mockDlcsPathTemplate;
        image.DegradedInfoJson = "https://mock.degraded.dlcs.io" + mockDlcsPathTemplate;
        image.ThumbnailInfoJson = "https://mock.thumbs.dlcs.io" + mockDlcsPathTemplate;
        image.Thumbnail400 = "https://mock.thumbs.dlcs.io" + mockDlcsPathTemplate + "/full/400,/0/default.jpg";
        image.Created = created;
        image.Origin = origin;
        image.InitialOrigin = initialOrigin;
        image.Width = width;
        image.Height = height;
        image.MaxUnauthorised = maxUnauthorised;
        image.Queued = queued;
        image.Dequeued = dequeued;
        image.Finished = finished;
        image.Ingesting = ingesting;
        image.Error = error;
        image.Tags = tags;
        image.String1 = string1;
        image.String2 = string2;
        image.String3 = string3;
        image.Number1 = number1;
        image.Number2 = number2;
        image.Number3 = number3;
        image.ImageOptimisationPolicy = imageOptimisationPolicy;
        image.ThumbnailPolicy = thumbnailPolicy;
        return image;
    }
    
    public static NamedQuery MakeNamedQuery(
        string baseUrl, int customerId, string modelId,
        string name, bool global, string template)
    {
        return new NamedQuery(baseUrl, customerId, modelId)
        {
            Name = name,
            Global = global,
            Template = template
        };
    }
             
    public static OriginStrategy MakeOriginStrategy(
        string baseUrl, string originStrategyId, string name, bool requiresCredentials)
    {
        return new OriginStrategy(baseUrl, originStrategyId)
        {
            Name = name,
            RequiresCredentials = requiresCredentials
        };
    }

    public static PortalUser MakePortalUser(
        string baseUrl, int customerId, string userId,
        string email, DateTime created, bool enabled)
    {
        return new PortalUser(baseUrl, customerId, userId)
        {
            Email = email,
            Created = created,
            Enabled = enabled
        };
    }
    
    public static Role MakeRole(
        string baseUrl, int customerId, string roleId, string name,
        string label, string[] aliases)
    {
        return new Role(baseUrl, customerId, roleId)
        {
            Name = name,
            Label = label,
            Aliases = aliases
        };
    }
    
    public static RoleProvider MakeRoleProvider(
        string baseUrl, int customerId, string authServiceId,
        string configuration, string credentials)
    {
        return new RoleProvider(baseUrl, customerId, authServiceId)
        {
            Configuration = configuration,
            Credentials = credentials
        };
    }

    public static Space MakeSpace(
        string baseUrl, int modelId, int customerId,
        string name, DateTime? created, string[]? defaultTags, int? maxUnauthorised)
    {
        return new Space(baseUrl, modelId, customerId)
        {

            Name = name,
            Created = created,
            DefaultTags = defaultTags,
            MaxUnauthorised = maxUnauthorised
        };
    }

    public static ThumbnailPolicy MakeThumbnailPolicy(
        string baseUrl, string thumbnailPolicyId, string name, int[] sizes)
    {
        return new ThumbnailPolicy(baseUrl, thumbnailPolicyId)
        {
            Name = name,
            Sizes = sizes
        };
    }
}