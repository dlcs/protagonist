using System.IO;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries;
using Orchestrator.Settings;

namespace Orchestrator.Features.PDF
{
    public class PdfNamedQueryService
    {
        private readonly IBucketReader bucketReader;
        private readonly IPdfCreator pdfCreator;
        private readonly ILogger<PdfNamedQueryService> logger;
        private readonly NamedQuerySettings namedQuerySettings;

        public PdfNamedQueryService(
            IBucketReader bucketReader,
            IOptions<NamedQuerySettings> namedQuerySettings,
            IPdfCreator pdfCreator,
            ILogger<PdfNamedQueryService> logger)
        {
            this.bucketReader = bucketReader;
            this.pdfCreator = pdfCreator;
            this.namedQuerySettings = namedQuerySettings.Value;
            this.logger = logger;
        }

        /// <summary>
        /// Get <see cref="PdfResult"/> containing PDF stream and status for specific named query result.
        /// </summary>
        public async Task<PdfResult> GetPdfResults(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult)
        {
            namedQueryResult.ParsedQuery.ThrowIfNull(nameof(namedQueryResult.ParsedQuery));
            
            var parsedNamedQuery = namedQueryResult.ParsedQuery!;

            // Check to see if we can use an existing PDF
            var pdfResult = await TryGetExistingPdf(parsedNamedQuery);

            // If it's Found or InProcess then no further processing for now
            if (pdfResult.Status is PdfStatus.Available or PdfStatus.InProcess)
            {
                return pdfResult;
            }
            
            var imageResults = await namedQueryResult.Results.ToListAsync();
            if (imageResults.Count == 0)
            {
                logger.LogWarning("No results found for PDF file {PdfS3Key}, aborting", parsedNamedQuery.PdfStorageKey);
                return new PdfResult(Stream.Null, PdfStatus.NotFound);
            }

            var success = await pdfCreator.CreatePdf(namedQueryResult.ParsedQuery, imageResults);
            if (!success) return new PdfResult(Stream.Null, PdfStatus.Error);
            
            var pdf = await LoadPdfObject(parsedNamedQuery.PdfStorageKey);
            if (pdf.Stream != null && pdf.Stream != Stream.Null)
            {
                return new(pdf.Stream!, PdfStatus.Available);
            }

            logger.LogWarning("PDF file {PdfS3Key} was successfully created but now cannot be loaded",
                parsedNamedQuery.PdfStorageKey);
            return new(Stream.Null, PdfStatus.Error);
        }
        
        /// <summary>
        /// Get <see cref="PdfControlFile"/> stored as specified key.
        /// </summary>
        public async Task<PdfControlFile?> GetPdfControlFile(string controlFileKey)
        {
            var pdfControlObject = await LoadPdfObject(controlFileKey);
            if (pdfControlObject.Stream == Stream.Null) return null;
            return await pdfControlObject.DeserializeFromJson<PdfControlFile>();
        }

        private async Task<PdfResult> TryGetExistingPdf(PdfParsedNamedQuery parsedNamedQuery)
        {
            var pdfControlFile = await GetPdfControlFile(parsedNamedQuery.ControlFileStorageKey);
            if (pdfControlFile == null) return new(Stream.Null, PdfStatus.NotFound);

            var pdfKey = parsedNamedQuery.PdfStorageKey;

            if (pdfControlFile.IsStale(namedQuerySettings.PdfControlStaleSecs))
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but it is stale. Will recreate",
                    pdfKey);
                return new(Stream.Null, PdfStatus.NotFound);
            }

            if (pdfControlFile.InProcess)
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but it's in progress", pdfKey);
                return new(Stream.Null, PdfStatus.InProcess);
            }

            var pdf = await LoadPdfObject(pdfKey);
            if (pdf.Stream != null && pdf.Stream != Stream.Null)
            {
                return new(pdf.Stream!, PdfStatus.Available);
            }

            logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate", pdfKey);
            return new(Stream.Null, PdfStatus.NotFound);
        }

        private Task<ObjectFromBucket> LoadPdfObject(string key)
        {
            var objectInBucket = new ObjectInBucket(namedQuerySettings.PdfBucket, key);
            return bucketReader.GetObjectFromBucket(objectInBucket);
        }
    }

    public record PdfResult(Stream? Stream, PdfStatus Status);
}