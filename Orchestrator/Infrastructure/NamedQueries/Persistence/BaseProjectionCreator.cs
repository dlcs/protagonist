using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence
{
    public abstract class BaseProjectionCreator<T> : IProjectionCreator<T>
        where T : StoredParsedNamedQuery
    {
        protected readonly IBucketReader BucketReader;
        protected readonly IBucketWriter BucketWriter;
        protected readonly ILogger Logger;
        protected readonly NamedQuerySettings NamedQuerySettings;
        protected readonly IStorageKeyGenerator StorageKeyGenerator;

        public BaseProjectionCreator(
            IBucketReader bucketReader,
            IBucketWriter bucketWriter,
            IOptions<NamedQuerySettings> namedQuerySettings,
            IStorageKeyGenerator storageKeyGenerator,
            ILogger logger)
        {
            BucketReader = bucketReader;
            BucketWriter = bucketWriter;
            Logger = logger;
            NamedQuerySettings = namedQuerySettings.Value;
            StorageKeyGenerator = storageKeyGenerator;
        }
        
        public async Task<bool> PersistProjection(T parsedNamedQuery, List<Asset> images,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var controlFile = await CreateControlFile(images, parsedNamedQuery);

                var createResponse = await CreateFile(parsedNamedQuery, images, cancellationToken);

                if (!createResponse.Success)
                {
                    return false;
                }

                controlFile.Exists = true;
                controlFile.InProcess = false;
                controlFile.SizeBytes = createResponse.Size;
                await UpdateControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating named query projection: {S3Key}", parsedNamedQuery.StorageKey);
                return false;
            }
        }
        
        private async Task<ControlFile> CreateControlFile(List<Asset> enumeratedResults,
            StoredParsedNamedQuery parsedNamedQuery)
        {
            Logger.LogInformation("Creating new control file: {ControlS3Key}", parsedNamedQuery.ControlFileStorageKey);
            var controlFile = new ControlFile
            {
                Created = DateTime.UtcNow,
                Key = parsedNamedQuery.StorageKey,
                Exists = false,
                InProcess = true,
                ItemCount = enumeratedResults.Count,
                SizeBytes = 0
            };

            await UpdateControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
            return controlFile;
        }

        private Task UpdateControlFile(string controlFileKey, ControlFile? controlFile) =>
            BucketWriter.WriteToBucket(StorageKeyGenerator.GetOutputLocation(controlFileKey),
                JsonConvert.SerializeObject(controlFile), "application/json");

        protected abstract Task<CreateProjectionResult> CreateFile(T parsedNamedQuery, List<Asset> assets,
            CancellationToken cancellationToken);
    }
}