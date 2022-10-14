using System;
using System.Net.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration.Infrastructure;

namespace Test.Helpers.Integration;

public static class ProtagonistAppFactoryX
{
    /// <summary>
    /// Configure app factory to use connection string from DBFixture and configure <see cref="TestAuthHandler"/> for
    /// auth
    /// </summary>
    public static HttpClient ConfigureBasicAuthedIntegrationTestHttpClient<T>(
        this ProtagonistAppFactory<T> factory,
        DlcsDatabaseFixture dbFixture,
        string authenticationScheme) where T : class
        => ConfigureBasicAuthedIntegrationTestHttpClient(factory, dbFixture, authenticationScheme, null);

    /// <summary>
    /// Configure app factory to use connection string from DBFixture and configure <see cref="TestAuthHandler"/> for
    /// auth. Takes an additional delegate to do additional setup
    /// </summary>
    public static HttpClient ConfigureBasicAuthedIntegrationTestHttpClient<T>(
        this ProtagonistAppFactory<T> factory,
        DlcsDatabaseFixture dbFixture,
        string authenticationScheme,
        Func<ProtagonistAppFactory<T>, ProtagonistAppFactory<T>> additionalSetup) where T : class
    {
        additionalSetup ??= f => f;
        
        var configuredFactory = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddAuthentication(authenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        authenticationScheme, _ => { });
            });

        var httpClient = additionalSetup(configuredFactory)
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        return httpClient;
    }
}