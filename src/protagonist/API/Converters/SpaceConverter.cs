using System;
using System.Linq;
using DLCS.HydraModel;

namespace API.Converters;

/// <summary>
/// Conversion between API and EF model forms of resources.
/// </summary>
public static class SpaceConverter
{
    /// <summary>
    /// Converts the EF model object to an API resource.
    /// </summary>
    /// <param name="dbSpace"></param>
    /// <param name="baseUrl"></param>
    /// <returns></returns>
    public static Space ToHydra(this DLCS.Model.Spaces.Space dbSpace, string baseUrl)
    {
        var space = new Space(baseUrl, dbSpace.Id, dbSpace.Customer)
        {
            Name = dbSpace.Name,
            Created = dbSpace.Created,
            DefaultTags = dbSpace.Tags,
            DefaultRoles = dbSpace.Roles,
            MaxUnauthorised = dbSpace.MaxUnauthorised,
            ApproximateNumberOfImages = dbSpace.ApproximateNumberOfImages
        };
        return space;
    }

}