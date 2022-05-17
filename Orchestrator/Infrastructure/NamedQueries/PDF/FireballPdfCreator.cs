using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Orchestrator.Infrastructure.NamedQueries.Persistence;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.PDF
{
    /// <summary>
    /// Use Fireball for projection of NamedQuery to PDF file
    /// </summary>
    /// <remarks>See https://github.com/fractos/fireball</remarks>
    public class FireballPdfCreator : BaseProjectionCreator<PdfParsedNamedQuery>
    {
        private const string PdfEndpoint = "pdf";
        private readonly HttpClient fireballClient;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        public FireballPdfCreator(
            IBucketReader bucketReader,
            IBucketWriter bucketWriter,
            IOptions<NamedQuerySettings> namedQuerySettings,
            ILogger<FireballPdfCreator> logger,
            HttpClient fireballClient,
            IBucketKeyGenerator bucketKeyGenerator
        ) : base(bucketReader, bucketWriter, namedQuerySettings, bucketKeyGenerator, logger)
        {
            this.fireballClient = fireballClient;
            jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        protected override async Task<CreateProjectionResult> CreateFile(PdfParsedNamedQuery parsedNamedQuery,
            List<Asset> assets, CancellationToken cancellationToken)
        {
            var pdfKey = parsedNamedQuery.StorageKey;

            try
            {
                Logger.LogInformation("Creating new pdf document at {PdfS3Key}", pdfKey);
                var playbook = GeneratePlaybook(pdfKey, parsedNamedQuery, assets);

                var fireballResponse = await CallFireball(cancellationToken, playbook, pdfKey);
                return fireballResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Http exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unknown exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
            }

            return new CreateProjectionResult();
        }

        private FireballPlaybook GeneratePlaybook(string pdfKey, PdfParsedNamedQuery parsedNamedQuery,
            List<Asset> assets)
        {
            var playbook = new FireballPlaybook
            {
                Output = BucketKeyGenerator.GetOutputLocation(pdfKey).GetS3Uri(),
                Title = parsedNamedQuery.ObjectName,
                CustomTypes = new FireballCustomTypes
                {
                    RedactedMessage = new FireballMessageProp { Message = parsedNamedQuery.RedactedMessage }
                }
            };

            playbook.Pages.Add(FireballPage.Download(parsedNamedQuery.CoverPageUrl));

            int pageNumber = 0;
            foreach (var i in NamedQueryProjections.GetOrderedAssets(assets, parsedNamedQuery))
            {
                Logger.LogDebug("Adding PDF page {PdfPage} to {PdfS3Key} for {Image}", pageNumber++, pdfKey, i.Id);
                if (i.Roles.HasText())
                {
                    Logger.LogDebug("Image {Image} on page {PdfPage} of {PdfS3Key} has roles, redacting", i.Id,
                        pageNumber++, pdfKey);
                    playbook.Pages.Add(FireballPage.Redacted());
                }
                else
                {
                    var largestThumb = BucketKeyGenerator.GetLargestThumbnailLocation(i.GetAssetId());
                    playbook.Pages.Add(FireballPage.Image(largestThumb.GetS3Uri()));
                }
            }

            return playbook;
        }
        
        private async Task<CreateProjectionResult?> CallFireball(CancellationToken cancellationToken, FireballPlaybook playbook, string pdfKey)
        {
            var jsonString = JsonConvert.SerializeObject(playbook, jsonSerializerSettings);
            var request = new HttpRequestMessage(HttpMethod.Post, PdfEndpoint)
            {
                Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
            };
            Logger.LogInformation("Calling fireball to create new pdf document at {PdfS3Key}", pdfKey);
            var sw = Stopwatch.StartNew();
            var response = await fireballClient.SendAsync(request, cancellationToken);
            var fireballResponse = await response.ReadAsJsonAsync<CreateProjectionResult>(true, jsonSerializerSettings);
            sw.Stop();
            Logger.LogInformation("Created new pdf document at {PdfS3Key} with size in bytes = {SizeBytes}. Took {Elapsed}ms",
                pdfKey, fireballResponse?.Size ?? -1, sw.ElapsedMilliseconds);
            return fireballResponse;
        }
    }

    public class FireballPlaybook
    {
        public string Method { get; set; } = "s3";  // TODO - should this have any say in prefix for adding low.jpg
        
        public string Output { get; set; }
        
        public string Title { get; set; }
        
        public FireballCustomTypes CustomTypes { get; set; }

        public List<FireballPage> Pages { get; set; } = new();
    }
    
    public class FireballCustomTypes
    {
        [JsonProperty("redacted")] 
        public FireballMessageProp RedactedMessage { get; set; }

        [JsonProperty("missing")] 
        public FireballMessageProp MissingMessage { get; set; } = new() { Message = "Unable to display this page" };
    }

    public class FireballMessageProp
    {
        public string Message { get; set; }
    }

    public class FireballPage
    {
        public string Type { get; set; }
        
        public string? Method { get; set; }
        
        public string? Input { get; set; }

        public static FireballPage Redacted() => new() { Type = "redacted" };

        public static FireballPage Download(string url) =>
            new() { Type = "pdf", Method = "download", Input = url };
        
        public static FireballPage Image(string url) =>
            new() { Type = "jpg", Method = "s3", Input = url };
    }
}