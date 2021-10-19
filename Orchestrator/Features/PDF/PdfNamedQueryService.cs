using System;
using System.IO;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;

namespace Orchestrator.Features.PDF
{
    public class PdfNamedQueryService
    {
        private readonly IBucketReader bucketReader;
        private readonly PdfCreator pdfCreator;
        private readonly ILogger<PdfNamedQueryService> logger;
        private readonly NamedQuerySettings namedQuerySettings;

        public PdfNamedQueryService(
            IBucketReader bucketReader,
            IOptions<NamedQuerySettings> namedQuerySettings,
            PdfCreator pdfCreator,
            ILogger<PdfNamedQueryService> logger)
        {
            this.bucketReader = bucketReader;
            this.pdfCreator = pdfCreator;
            this.namedQuerySettings = namedQuerySettings.Value;
            this.logger = logger;
        }

        public async Task<PdfResult> GetPdfResults(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult,
            string queryName)
        {
            namedQueryResult.Query.ThrowIfNull(nameof(namedQueryResult.Query));

            var pdfResult = await TryGetExistingPdf(namedQueryResult, queryName);

            // If it's Found or InProcess then no further processing for now
            if (pdfResult.Status is PdfStatus.Found or PdfStatus.InProcess)
            {
                return pdfResult;
            }

            return await pdfCreator.CreatePdf(namedQueryResult, queryName);
        }

        public async Task<PdfControlFile?> GetPdfControlFile(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult, string queryName)
        {
            var pdfControlObject = await GetPdfObjectForQuery(namedQueryResult.Query!, queryName, true);
            if (pdfControlObject.Stream == Stream.Null) return null;
            return await pdfControlObject.DeserializeFromJson<PdfControlFile>();
        }

        private async Task<PdfResult> TryGetExistingPdf(
            NamedQueryResult<PdfParsedNamedQuery> namedQueryResult,
            string queryName)
        {
            var pdfControlFile = await GetPdfControlFile(namedQueryResult, queryName);
            if (pdfControlFile == null) return new(Stream.Null, PdfStatus.NotFound);

            if (pdfControlFile.IsStale(namedQuerySettings.PdfControlStaleSecs))
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate",
                    GetPdfKey(namedQueryResult.Query!, queryName));
                return new(Stream.Null, PdfStatus.NotFound);
            }

            if (pdfControlFile.InProcess)
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate",
                    GetPdfKey(namedQueryResult.Query!, queryName));
                return new(Stream.Null, PdfStatus.InProcess);
            }

            // return PDF
            var pdf = await GetPdfObjectForQuery(namedQueryResult.Query!, queryName, false);
            if (pdf.Stream != null && pdf.Stream != Stream.Null)
            {
                return new(pdf.Stream!, PdfStatus.Found);
            }

            logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate",
                GetPdfKey(namedQueryResult.Query!, queryName));
            return new(Stream.Null, PdfStatus.NotFound);
        }

        private Task<ObjectFromBucket> GetPdfObjectForQuery(PdfParsedNamedQuery parsedNamedQuery, string queryName, bool isControlFile)
        {
            var key = GetPdfKey(parsedNamedQuery, queryName);
            if (isControlFile) key += ".json";
            var objectInBucket = new ObjectInBucket(namedQuerySettings.PdfBucket, key);
            return bucketReader.GetObjectFromBucket(objectInBucket);
        }

        private string GetPdfKey(PdfParsedNamedQuery parsedNamedQuery, string queryName)
        {
            var key = namedQuerySettings.PdfControlFileTemplate
                .Replace("{customer}", parsedNamedQuery.Customer.ToString())
                .Replace("{queryname}", queryName)
                .Replace("{args}", string.Join("/", parsedNamedQuery.Args));
            return key;
        }
    }

    public enum PdfStatus
    {
        Found,
        InProcess,
        NotFound
    }

    public record PdfResult(Stream? Stream, PdfStatus Status);
    
    public class PdfControlFile
    {
        [JsonProperty("key")]
        public string Key { get; set; }
        
        [JsonProperty("exists")]
        public bool Exists { get; set; }
        
        [JsonProperty("inprocess")]
        public bool InProcess { get; set; }
        
        [JsonProperty("created")]
        public DateTime Created { get; set; }
        
        [JsonProperty("pageCount")]
        public int PageCount { get; set; }
        
        [JsonProperty("sizeBytes")]
        public int SizeBytes { get; set; }

        /// <summary>
        /// Check if this is control file is stale (in process for longer than X secs)
        /// </summary>
        public bool IsStale(int staleSecs)
            => InProcess && DateTime.Now.Subtract(Created).TotalSeconds > staleSecs;
    }

    public class PdfCreator
    {
        public Task<PdfResult> CreatePdf(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult, string queryName)
        {
            throw new NotImplementedException();
        }
    }
}