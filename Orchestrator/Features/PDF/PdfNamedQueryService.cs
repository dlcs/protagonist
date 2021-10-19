using System.IO;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
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

        public async Task<PdfResult> GetPdfResults(NamedQueryResult<PdfParsedNamedQuery> namedQueryResult,
            string queryName)
        {
            namedQueryResult.Query.ThrowIfNull(nameof(namedQueryResult.Query));
            
            var parsedNamedQuery = namedQueryResult.Query!;
            var controlFileKey = PdfNamedQueryPathHelpers.GetPdfKey(namedQuerySettings.PdfControlFileTemplate,
                namedQueryResult.Query, queryName, true);
            var pdfKey = PdfNamedQueryPathHelpers.GetPdfKey(namedQuerySettings.PdfControlFileTemplate,
                parsedNamedQuery, queryName, false);

            // Check to see if we can use an existing PDF
            var pdfResult = await TryGetExistingPdf(controlFileKey, pdfKey);

            // If it's Found or InProcess then no further processing for now
            if (pdfResult.Status is PdfStatus.Available or PdfStatus.InProcess)
            {
                return pdfResult;
            }

            var success = await pdfCreator.CreatePdf(namedQueryResult, queryName);
            if (!success) return new PdfResult(Stream.Null, PdfStatus.Error);
            
            var pdf = await LoadPdfObject(pdfKey);
            if (pdf.Stream != null && pdf.Stream != Stream.Null)
            {
                return new(pdf.Stream!, PdfStatus.Available);
            }

            logger.LogWarning("PDF file {PdfS3Key} was successfully created but now cannot be loaded", pdfKey);
            return new(Stream.Null, PdfStatus.Error);
        }

        private async Task<PdfControlFile?> GetPdfControlFile(string controlFileKey)
        {
            var pdfControlObject = await LoadPdfObject(controlFileKey);
            if (pdfControlObject.Stream == Stream.Null) return null;
            return await pdfControlObject.DeserializeFromJson<PdfControlFile>();
        }

        private async Task<PdfResult> TryGetExistingPdf(string controlFileKey, string pdfKey)
        {
            var pdfControlFile = await GetPdfControlFile(controlFileKey);
            if (pdfControlFile == null) return new(Stream.Null, PdfStatus.NotFound);
            
            if (pdfControlFile.IsStale(namedQuerySettings.PdfControlStaleSecs))
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate", pdfKey);
                return new(Stream.Null, PdfStatus.NotFound);
            }

            if (pdfControlFile.InProcess)
            {
                logger.LogWarning("PDF file {PdfS3Key} has valid control-file but PDF not found. Will recreate", pdfKey);
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