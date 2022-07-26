using System;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Core.Collections;
using DLCS.Web.Response;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace API.Client;

/// <summary>
/// Rather than just using Newtonsoft ReadAsJsonAsync directly, we wrap the deserialisation
/// to ensure a correct Hydra object with @context is created, or a DlcsException is thrown.
/// </summary>
public static class HydraHttpResponseMessageX
{
    public static async Task<T?> ReadAsHydraResponseAsync<T>(this HttpResponseMessage response,
        JsonSerializerSettings? settings = null)
    {
        if ((int)response.StatusCode < 400)
        {
            return await response.ReadWithHydraContext<T>(true, settings);
        }

        Error? error;
        try
        {
            error = await response.ReadAsJsonAsync<Error>(false, settings);
        }
        catch (Exception ex)
        {
            throw new DlcsException("Could not find a Hydra error in response", ex);
        }

        if (error != null)
        {
            throw new DlcsException(error.Description);
        }

        throw new DlcsException("Unable to process error condition");
    }

    private static async Task<T?> ReadWithHydraContext<T>(
        this HttpResponseMessage response,
        bool ensureSuccess,
        JsonSerializerSettings? settings)
    {
        var json = await response.ReadAsJsonAsync<T>(ensureSuccess, settings);
        if (json is JsonLdBaseWithHydraContext hydra)
        {
            hydra.WithContext = true;
        }
        return json;
    }
}