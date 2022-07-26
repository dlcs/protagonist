using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace API.Auth;

public static class ServiceCollectionX
{
    /// <summary>
    /// Add DlcsDelegatedBasicAuthenticationHandler to services collection.
    /// </summary>
    public static AuthenticationBuilder AddDlcsDelegatedBasicAuth(this IServiceCollection services,
        Action<BasicAuthenticationOptions> configureOptions)
        => services
            .AddAuthentication(BasicAuthenticationDefaults.AuthenticationScheme)
            .AddScheme<BasicAuthenticationOptions, DlcsBasicAuthenticationHandler>(
                BasicAuthenticationDefaults.AuthenticationScheme, configureOptions);
}

/// <summary>
/// Options for use with BasicAuth handler.
/// </summary>
public class BasicAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Get or set the Realm for use in auth challenges.
    /// </summary>
    public string Realm { get; set; }
    public string Salt { get; set; }
}

/// <summary>
/// Contains constants for use with basic auth.
/// </summary>
public static class BasicAuthenticationDefaults
{
    public const string AuthenticationScheme = "Basic";
}