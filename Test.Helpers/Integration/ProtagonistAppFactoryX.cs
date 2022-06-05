using System.Net.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Test.Helpers.Integration.Infrastructure;

namespace Test.Helpers.Integration;

public static class ProtagonistAppFactoryX
{
    public static HttpClient ConfigureIntegrationTestClient<T>(
        this ProtagonistAppFactory<T> factory,
        DlcsDatabaseFixture dbFixture,
        string authenticationScheme) where T : class
    {
        var httpClient = factory
            .WithConnectionString(dbFixture.ConnectionString)
            .WithTestServices(services =>
            {
                services.AddAuthentication(authenticationScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        authenticationScheme, _ => { });
            })
            .CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        return httpClient;
    }
}