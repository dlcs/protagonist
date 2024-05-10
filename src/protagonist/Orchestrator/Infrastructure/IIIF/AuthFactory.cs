using System;
using System.Collections.Generic;
using DLCS.Core.Collections;
using DLCS.Core.Strings;
using DLCS.Model.Auth.Entities;
using IIIF;
using IIIF.Presentation.V2;
using IIIF.Presentation.V2.Strings;
using AuthCookie0 = IIIF.Auth.V0.AuthCookieService;
using AuthCookie1 = IIIF.Auth.V1.AuthCookieService;
using AuthLogout0 = IIIF.Auth.V0.AuthLogoutService;
using AuthLogout1 = IIIF.Auth.V1.AuthLogoutService;
using AuthToken0 = IIIF.Auth.V0.AuthTokenService;
using AuthToken1 = IIIF.Auth.V1.AuthTokenService;

namespace Orchestrator.Infrastructure.IIIF;

internal static class AuthFactory
{
    /// <summary>
    /// Get AuthCookieService for specified <see cref="AuthService"/>
    /// </summary>
    public static IService? ConvertToAuthCookieService(this AuthService authService, IList<string> nonStandard,
        bool throwIfUnknownProfile)
    {
        IService? converted = authService.Profile switch
        {
            AuthCookie1.ClickthroughProfile or AuthCookie1.ExternalProfile or AuthCookie1.KioskProfile
                or AuthCookie1.LoginProfile
                => new AuthCookie1(authService.Profile)
                {
                    Id = authService.Name,
                    Label = new MetaDataValue(authService.Label),
                    Description = new MetaDataValue(authService.Description),
                },
            AuthCookie0.ClickthroughProfile or AuthCookie0.ExternalProfile or AuthCookie0.KioskProfile
                or AuthCookie0.LoginProfile
                => GetAuthCookie0Service(authService),
            _ => null
        };

        return HandleConversion(authService, converted, throwIfUnknownProfile, nonStandard, true);
    }

    private static AuthCookie0 GetAuthCookie0Service(AuthService authService) 
        => new(authService.Profile)
        {
            Id = authService.Name,
            Label = new MetaDataValue(authService.Label),
            Description = new MetaDataValue(authService.Description),
        };

    /// <summary>
    /// Convert specified <see cref="AuthService"/> to a IIIF child auth service.
    /// The "Child" part makes this an access token service, logout service etc
    /// </summary>
    public static IService? ConvertToIIIFChildAuthService(this AuthService authService, string parentId,
        bool throwIfUnknownProfile)
    {
        IService? childService = authService.Profile switch
        {
            AuthLogout1.AuthLogout1Profile => new AuthLogout1 { Id = $"{parentId}/logout" },
            AuthToken1.AuthToken1Profile => new AuthToken1 { Id = authService.Name },
            AuthLogout0.AuthLogout0Profile => new AuthLogout0 { Id = $"{parentId}/logout" },
            AuthToken0.AuthToken0Profile => new AuthToken0 { Id = authService.Name },
            _ => null
        };

        return HandleConversion(authService, childService, throwIfUnknownProfile, null, false);
    }

    private static IService? HandleConversion(AuthService authService, IService? convertedService,
        bool throwIfUnknownProfile, IList<string>? nonStandard, bool isAccessCookie)
    {
        // If we don't have a service - is it a non-standard Profile?
        if (convertedService == null && !nonStandard.IsNullOrEmpty())
        {
            if (nonStandard.Contains(authService.Profile))
            {
                convertedService = GetAuthCookie0Service(authService);
            }
        }

        // If we still don't have a service bail out, or throw
        if (convertedService == null)
        {
            if (throwIfUnknownProfile)
            {
                throw new ArgumentException($"Unsupported AuthService profile type: {authService.Profile}");
            }

            return null;
        }

        if (isAccessCookie) return convertedService;
        
        if (authService.Label.HasText())
        {
            (convertedService as ResourceBase)!.Label = new MetaDataValue(authService.Label);
        }

        if (authService.Description.HasText())
        {
            (convertedService as ResourceBase)!.Description = new MetaDataValue(authService.Description);
        }

        return convertedService;
    }
}