using System.IO;
using System.Threading.Tasks;
using DLCS.Core.Guard;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence
{
    /// <summary>
    /// Service for handling NQ projections that are created and stored alongside a corresponding control-file.
    /// </summary>
    public class StoredNamedQueryService
    {
        private readonly IBucketReader bucketReader;
        private readonly ILogger<StoredNamedQueryService> logger;
        private readonly NamedQuerySettings namedQuerySettings;

        public StoredNamedQueryService(
            IBucketReader bucketReader,
            IOptions<NamedQuerySettings> namedQuerySettings,
            ILogger<StoredNamedQueryService> logger)
        {
            this.bucketReader = bucketReader;
            this.namedQuerySettings = namedQuerySettings.Value;
            this.logger = logger;
        }

        /// <summary>
        /// Get <see cref="StoredResult"/> containing data stream and status for specific named query result.
        /// </summary>
        public async Task<StoredResult> GetResults<T>(NamedQueryResult<T> namedQueryResult,
            IProjectionCreator<T> projectionCreator)
            where T : StoredParsedNamedQuery
        {
            namedQueryResult.ParsedQuery.ThrowIfNull(nameof(namedQueryResult.ParsedQuery));

            var parsedNamedQuery = namedQueryResult.ParsedQuery!;

            // Check to see if we can use an existing item
            var existingResult = await TryGetExistingResource(parsedNamedQuery);

            // If it's Found or InProcess then no further processing for now, returns what's found
            if (existingResult.Status is PersistedProjectionStatus.Available or PersistedProjectionStatus.InProcess)
            {
                return existingResult;
            }

            var imageResults = await namedQueryResult.Results.ToListAsync();
            if (imageResults.Count == 0)
            {
                logger.LogWarning("No results found for PDF file {PdfS3Key}, aborting", parsedNamedQuery.StorageKey);
                return new StoredResult(Stream.Null, PersistedProjectionStatus.NotFound);
            }

            // TODO Get IProjectionCreator for specified type here and call it
            // var success = await createResource(parsedNamedQuery, imageResults);
            var success = await projectionCreator.PersistProjection(parsedNamedQuery, imageResults);
            if (!success) return new StoredResult(Stream.Null, PersistedProjectionStatus.Error);

            var pdf = await LoadStoredObject(parsedNamedQuery.StorageKey);
            if (pdf.Stream != null && pdf.Stream != Stream.Null)
            {
                return new(pdf.Stream!, PersistedProjectionStatus.Available);
            }

            logger.LogWarning("File {S3Key} was successfully created but now cannot be loaded",
                parsedNamedQuery.StorageKey);
            return new(Stream.Null, PersistedProjectionStatus.Error);
        }

        /// <summary>
        /// Get <see cref="ControlFile"/> stored as specified key.
        /// </summary>
        public async Task<ControlFile?> GetControlFile(string controlFileKey)
        {
            var controlObject = await LoadStoredObject(controlFileKey);
            if (controlObject.Stream == Stream.Null) return null;
            return await controlObject.DeserializeFromJson<ControlFile>();
        }

        private async Task<StoredResult> TryGetExistingResource(StoredParsedNamedQuery parsedNamedQuery)
        {
            var controlFile = await GetControlFile(parsedNamedQuery.ControlFileStorageKey);
            if (controlFile == null) return new(Stream.Null, PersistedProjectionStatus.NotFound);

            var itemKey = parsedNamedQuery.StorageKey;

            if (controlFile.IsStale(namedQuerySettings.PdfControlStaleSecs)) // TODO - allow different values
            {
                logger.LogWarning("File {S3Key} has valid control-file but it is stale. Will recreate",
                    itemKey);
                return new(Stream.Null, PersistedProjectionStatus.NotFound);
            }

            if (controlFile.InProcess)
            {
                logger.LogWarning("File {S3Key} has valid control-file but it's in progress", itemKey);
                return new(Stream.Null, PersistedProjectionStatus.InProcess);
            }

            var resource = await LoadStoredObject(itemKey);
            if (resource.Stream != null && resource.Stream != Stream.Null)
            {
                return new(resource.Stream!, PersistedProjectionStatus.Available);
            }

            logger.LogWarning("File {S3Key} has valid control-file but item not found. Will recreate", itemKey);
            return new(Stream.Null, PersistedProjectionStatus.NotFound);
        }

        private Task<ObjectFromBucket> LoadStoredObject(string key)
        {
            var objectInBucket = new ObjectInBucket(namedQuerySettings.OutputBucket, key);
            return bucketReader.GetObjectFromBucket(objectInBucket);
        }
    }
    
    public record StoredResult(Stream? Stream, PersistedProjectionStatus Status);
}