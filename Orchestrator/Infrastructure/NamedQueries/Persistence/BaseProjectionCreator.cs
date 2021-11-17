using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Model.Storage;
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
        protected readonly ILogger Logger;
        protected readonly NamedQuerySettings NamedQuerySettings;

        public BaseProjectionCreator(
            IBucketReader bucketReader,
            IOptions<NamedQuerySettings> namedQuerySettings,
            ILogger logger)
        {
            BucketReader = bucketReader;
            Logger = logger;
            NamedQuerySettings = namedQuerySettings.Value;
        }
        
        public async Task<bool> PersistProjection(T parsedNamedQuery, List<Asset> images)
        {
            try
            {
                var controlFile = await CreateControlFile(images, parsedNamedQuery);

                var createResponse = await CreateFile(parsedNamedQuery, images);

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
                Created = DateTime.Now,
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
            BucketReader.WriteToBucket(new ObjectInBucket(NamedQuerySettings.OutputBucket, controlFileKey),
                JsonConvert.SerializeObject(controlFile), "application/json");

        protected abstract Task<CreateProjectionResult> CreateFile(T parsedNamedQuery, List<Asset> assets);
    }
}