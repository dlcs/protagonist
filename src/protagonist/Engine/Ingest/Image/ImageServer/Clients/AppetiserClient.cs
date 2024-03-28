using DLCS.Web.Requests;
using Engine.Ingest.Image.ImageServer.Models;
using Engine.Settings;
using Microsoft.Extensions.Options;

namespace Engine.Ingest.Image.ImageServer.Clients;

public class AppetiserClient : IAppetiserClient
{
        private HttpClient appetiserClient;
        private readonly EngineSettings engineSettings;
        private readonly ILogger<AppetiserClient> logger;
        
        public AppetiserClient(
                HttpClient appetiserClient,
                ILogger<AppetiserClient> logger,
                IOptionsMonitor<EngineSettings> engineOptionsMonitor)
        {
                this.appetiserClient = appetiserClient;
                this.logger = logger;
                engineSettings = engineOptionsMonitor.CurrentValue;
        }

        public async Task<IAppetiserResponse> CallAppetiser(
                AppetiserRequestModel requestModel
                , CancellationToken cancellationToken = default)
        {
                using var request = new HttpRequestMessage(HttpMethod.Post, "convert");
                IAppetiserResponse? responseModel;
                request.SetJsonContent(requestModel);

                if (engineSettings.ImageIngest.ImageProcessorDelayMs > 0)
                {
                        await Task.Delay(engineSettings.ImageIngest.ImageProcessorDelayMs);
                }

                using var response = await appetiserClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                        responseModel = await response.Content.ReadFromJsonAsync<AppetiserResponseModel>(cancellationToken: cancellationToken);
                }
                else
                {
                        responseModel =
                                await response.Content.ReadFromJsonAsync<AppetiserResponseErrorModel>(
                                        cancellationToken: cancellationToken);
                }
                
                return responseModel;
        }
}