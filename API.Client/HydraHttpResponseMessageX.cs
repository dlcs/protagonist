using System;
using System.Net.Http;
using System.Threading.Tasks;
using DLCS.Web.Response;
using Hydra.Model;
using Newtonsoft.Json;

namespace API.Client
{
    public static class HydraHttpResponseMessageX
    {
        public static async Task<T?> ReadAsHydraResponseAsync<T>(this HttpResponseMessage response,
            JsonSerializerSettings? settings = null)
        {
            if ((int) response.StatusCode < 400)
            {
                return await response.ReadAsJsonAsync<T>(true, settings);
            }

            Error? error;
            try
            {
                error = await response.ReadAsJsonAsync<Error>(false, settings);
            }
            catch(Exception ex)
            {
                throw new DlcsException("Could not find a Hydra error in response", ex);
            }
            if (error != null)
            {
                throw new DlcsException(error.Description);
            }
            throw new DlcsException("Unable to process error condition");
        }
    }
}