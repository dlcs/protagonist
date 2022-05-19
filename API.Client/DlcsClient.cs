﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using DLCS.HydraModel;
using DLCS.Web.Auth;
using DLCS.Web.Response;
using Hydra;
using Hydra.Collections;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace API.Client
{
    /// <summary>
    /// Client for Dlcs API
    /// </summary>
    public class DlcsClient : IDlcsClient
    {
        private readonly ILogger<DlcsClient> logger;
        private readonly HttpClient httpClient;
        private readonly ClaimsPrincipal currentUser;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        public DlcsClient(
            ILogger<DlcsClient> logger,
            HttpClient httpClient,
            ClaimsPrincipal currentUser)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            this.currentUser = currentUser;

            var basicAuth = currentUser.GetApiCredentials();
            if (!string.IsNullOrEmpty(basicAuth))
            {
                this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            }
            jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public async Task<HydraCollection<Space>?> GetSpaces(int page, int pageSize, int? customerId = null)
        {
            customerId ??= currentUser.GetCustomerId();
            var url = $"/customers/{customerId}/spaces?page={page}&pageSize={pageSize}";
            var response = await httpClient.GetAsync(url);
            var space = await response.ReadAsHydraResponseAsync<HydraCollection<Space>>(jsonSerializerSettings);
            return space;
        }

        public async Task<Space?> GetSpaceDetails(int spaceId)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces/{spaceId}";
            var response = await httpClient.GetAsync(url);
            var space = await response.ReadAsHydraResponseAsync<Space>(jsonSerializerSettings);
            return space;
        }
        
        
        public async Task<HydraCollection<Image>> GetSpaceImages(int page, int pageSize, int spaceId, string? orderBy = null)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces/{spaceId}/images?page={page}&pageSize={pageSize}";
            if (orderBy != null)
            {
                url = $"{url}&orderBy={orderBy}";
            }
            var response = await httpClient.GetAsync(url);
            var images = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>(jsonSerializerSettings);
            return images;
        }

        public async Task<Space?> CreateSpace(Space newSpace)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces";
            var response = await httpClient.PostAsync(url, ApiBody(newSpace));
            var space = await response.ReadAsHydraResponseAsync<Space>(jsonSerializerSettings);
            return space;
        }

        public async Task<Space?> PatchSpace(int spaceId, Space space)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces/{spaceId}";
            var response = await httpClient.PatchAsync(url, ApiBody(space));
            var patchedSpace = await response.ReadAsHydraResponseAsync<Space>(jsonSerializerSettings);
            return patchedSpace;
        }

        public async Task<IEnumerable<string>?> GetApiKeys()
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/keys";
            var response = await httpClient.GetAsync(url);
            var apiKeys = await response.ReadAsHydraResponseAsync<HydraCollection<ApiKey>>(jsonSerializerSettings);
            return apiKeys?.Members.Select(m => m.Key);
        }

        public async Task<ApiKey> CreateNewApiKey()
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/keys";
            var response = await httpClient.PostAsync(url, null!);
            var apiKey = await response.ReadAsHydraResponseAsync<ApiKey>(jsonSerializerSettings);
            return apiKey;
        }

        public async Task<Image> GetImage(int requestSpaceId, string requestImageId)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/spaces/{requestSpaceId}/images/{requestImageId}";
            var response = await httpClient.GetAsync(url);
            var image = await response.ReadAsHydraResponseAsync<Image>(jsonSerializerSettings);
            return image;
        }
        
        public async Task<HydraCollection<PortalUser>?> GetPortalUsers()
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/portalUsers";
            var response = await httpClient.GetAsync(url);
            var portalUsers = await response.ReadAsHydraResponseAsync<HydraCollection<PortalUser>>(jsonSerializerSettings);
            return portalUsers;
        }

        public async Task<PortalUser> CreatePortalUser(PortalUser portalUser)
        {
            // TODO - a 400 - badRequest is likely an email address that already exists.
            // Handle that with some sort of nicer response code or known error enum.
            var url = $"/customers/{currentUser.GetCustomerId()}/portalUsers";
            var response = await httpClient.PostAsync(url, ApiBody(portalUser));
            var newUser = await response.ReadAsHydraResponseAsync<PortalUser>(jsonSerializerSettings);
            return newUser;
        }

        public async Task<bool> DeletePortalUser(string portalUserId)
        {
            var url = $"/customers/{currentUser.GetCustomerId()}/portalUsers/{portalUserId}";
            try
            {
                var response = await httpClient.DeleteAsync(url);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting portalUser '{PortalUserId}'", portalUserId);
                return false;
            }
        }

        public async Task<Image?> DirectIngestImage(int spaceId, string imageId, Image asset)
        {
            // TODO - error handling
            var uri = $"/customers/{currentUser.GetCustomerId()}/spaces/{spaceId}/images/{imageId}";
            var response = await httpClient.PutAsync(uri, ApiBody(asset));
            return await response.ReadAsHydraResponseAsync<Image>(jsonSerializerSettings);
        }

        public async Task<HydraCollection<Image>> PatchImages(HydraCollection<Image> images, int spaceId)
        {
            int? customerId = currentUser.GetCustomerId();
            string uri = $"/customers/{customerId}/spaces/{spaceId}/images";
            // The old API call (e.g., in Wellcome) required incoming images to have the ModelId
            // matching the DB ID, e.g., /2/1/my-image-id
            // But this is not right; the DLCS should construct that DB ID from cust, space, and "modelId"
            // So where on this payload is the DLCS going to get that information?
            // From the URL target of the operation:
            // For this bulk patch targeted at /customers/{customerId}/spaces/{spaceId}/images, 
            // you can't change {customerId} of the images you are sending (disallowed anyway).
            // And while you could in theory change the space, you'd end up with the database ID having a different 
            // space "slot" (2/1/my-image-id) from its space property, which is undesirable.
            // We'd need a new ID, so there needs to be a dedicated MOVE operation that will create a new resource.
            // 
            // Therefore we are NOT going to do this:    (delete from existing impls)
            // foreach (var image in images.Members)
            // {
            //    image.ModelId = $"{customerId}/{spaceId}/{image.ModelId}";
            // }
            // ...DLCS will find the images to patch that match {customerId} and {spaceId}
            var response = await httpClient.PatchAsync(uri, ApiBody(images));
            var patched = await response.ReadAsHydraResponseAsync<HydraCollection<Image>>(jsonSerializerSettings);
            return patched;
        }

        public Task<bool> ReingestAsset(int requestSpaceId, string requestImageId)
        {
            throw new NotImplementedException();
        }

        private HttpContent ApiBody(JsonLdBase apiObject)
        {
            var jsonString = JsonConvert.SerializeObject(apiObject, jsonSerializerSettings);
            return new StringContent(jsonString, Encoding.UTF8, "application/json");
        }
    }
}