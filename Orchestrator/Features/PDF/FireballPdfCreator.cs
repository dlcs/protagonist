﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
using DLCS.Repository.Storage;
using DLCS.Web.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;

namespace Orchestrator.Features.PDF
{
    /// <summary>
    /// Implementation of <see cref="IPdfCreator"/> using Fireball for generation of PDF file.
    /// </summary>
    /// <remarks>See https://github.com/fractos/fireball</remarks>
    public class FireballPdfCreator : IPdfCreator
    {
        private const string PdfEndpoint = "pdf";
        private readonly IBucketReader bucketReader;
        private readonly ILogger<FireballPdfCreator> logger;
        private readonly HttpClient fireballClient;
        private readonly NamedQuerySettings namedQuerySettings;
        private readonly JsonSerializerSettings jsonSerializerSettings;

        public FireballPdfCreator(
            IBucketReader bucketReader,
            IOptions<NamedQuerySettings> namedQuerySettings,
            ILogger<FireballPdfCreator> logger,
            HttpClient fireballClient
        )
        {
            this.bucketReader = bucketReader;
            this.logger = logger;
            this.fireballClient = fireballClient;
            this.namedQuerySettings = namedQuerySettings.Value;
            jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public async Task<bool> CreatePdf(PdfParsedNamedQuery parsedNamedQuery, List<Asset> images)
        {
            // TODO - there is a slim chance of this being double triggered
            try
            {
                var controlFile = await CreateControlFile(images, parsedNamedQuery);

                var fireballResponse = await CreatePdfFile(parsedNamedQuery, images);

                if (!fireballResponse.Success)
                {
                    return false;
                }

                controlFile.Exists = true;
                controlFile.InProcess = false;
                controlFile.SizeBytes = fireballResponse.Size;
                await UpdatePdfControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating PDF: {PdfS3Key}", parsedNamedQuery.PdfStorageKey);
                return false;
            }
        }

        private async Task<PdfControlFile> CreateControlFile(List<Asset> enumeratedResults,
            PdfParsedNamedQuery parsedNamedQuery)
        {
            logger.LogInformation("Creating new pdf-control file at {PdfControlS3Key}",
                parsedNamedQuery.ControlFileStorageKey);
            var controlFile = new PdfControlFile
            {
                Created = DateTime.Now,
                Key = parsedNamedQuery.PdfStorageKey,
                Exists = false,
                InProcess = true,
                PageCount = enumeratedResults.Count,
                SizeBytes = 0
            };

            await UpdatePdfControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
            return controlFile;
        }

        private Task UpdatePdfControlFile(string controlFileKey, PdfControlFile? controlFile) =>
            bucketReader.WriteToBucket(new ObjectInBucket(namedQuerySettings.PdfBucket, controlFileKey),
                JsonConvert.SerializeObject(controlFile), "application/json");

        private async Task<FireballResponse> CreatePdfFile(PdfParsedNamedQuery? parsedNamedQuery,
            List<Asset> enumeratedResults)
        {
            var pdfKey = parsedNamedQuery.PdfStorageKey;

            try
            {
                logger.LogInformation("Creating new pdf document at {PdfS3Key}", pdfKey);
                var playbook = GeneratePlaybook(pdfKey, parsedNamedQuery, enumeratedResults);

                var jsonString = JsonConvert.SerializeObject(playbook, jsonSerializerSettings);
                var request = new HttpRequestMessage(HttpMethod.Post, PdfEndpoint)
                {
                    Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
                };
                var response = await fireballClient.SendAsync(request);
                var fireballResponse = await response.ReadAsJsonAsync<FireballResponse>(true, jsonSerializerSettings);
                logger.LogInformation("Created new pdf document at {PdfS3Key} with size in bytes = {SizeBytes}",
                    pdfKey, fireballResponse?.Size ?? -1);
                return fireballResponse;
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Http exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unknown exception calling fireball to generate PDF {PdfS3Key}", pdfKey);
            }

            return new FireballResponse();
        }

        private FireballPlaybook GeneratePlaybook(string? pdfKey, PdfParsedNamedQuery? parsedNamedQuery,
            List<Asset>? enumeratedResults)
        {
            var playbook = new FireballPlaybook
            {
                Output = $"s3://{namedQuerySettings.PdfBucket}/{pdfKey}",
                Title = parsedNamedQuery.ObjectName,
                CustomTypes = new FireballCustomTypes
                {
                    RedactedMessage = new FireballMessageProp { Message = parsedNamedQuery.RedactedMessage }
                }
            };

            playbook.Pages.Add(FireballPage.Download(parsedNamedQuery.CoverPageUrl));

            int pageNumber = 0;
            foreach (var i in enumeratedResults.OrderBy(i =>
                NamedQueryProjections.GetCanvasOrderingElement(i, parsedNamedQuery)))
            {
                logger.LogDebug("Adding PDF page {PdfPage} to {PdfS3Key} for {Image}", pageNumber++, pdfKey, i.Id);
                if (i.Roles.HasText())
                {
                    logger.LogDebug("Image {Image} on page {PdfPage} of {PdfS3Key} has roles, redacting", i.Id,
                        pageNumber++, pdfKey);
                    playbook.Pages.Add(FireballPage.Redacted());
                }
                else
                {
                    playbook.Pages.Add(
                        FireballPage.Image($"s3://{namedQuerySettings.ThumbsBucket}/{i.GetStorageKey()}/low.jpg"));
                }
            }

            return playbook;
        }
    }

    public class FireballPlaybook
    {
        public string Method { get; set; } = "s3";
        
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

    public class FireballResponse
    {
        public bool Success { get; set; }
        
        public int Size { get; set; }
    }
}