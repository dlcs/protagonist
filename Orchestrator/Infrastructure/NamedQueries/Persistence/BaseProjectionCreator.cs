using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.Core.Collections;
using DLCS.Model.Assets;
using DLCS.Model.Assets.NamedQueries;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orchestrator.Infrastructure.NamedQueries.Persistence.Models;
using Orchestrator.Settings;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence
{
    /// <summary>
    /// Base class that manages creation and updating of <see cref="ControlFile"/>, creation of projected file delegated
    /// to inheriting class 
    /// </summary>
    /// <typeparam name="T">Type of NQ handled</typeparam>
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
        
        public async Task<(bool success, ControlFile? controlFile)> PersistProjection(T parsedNamedQuery, List<Asset> images,
            CancellationToken cancellationToken = default)
        {
            ControlFile? controlFile = null;
            try
            {
                controlFile = await CreateControlFile(images, parsedNamedQuery);

                var createResponse = await CreateFile(parsedNamedQuery, images, cancellationToken);

                if (!createResponse.Success)
                {
                    return (false, controlFile);
                }

                controlFile.Exists = true;
                controlFile.InProcess = false;
                controlFile.SizeBytes = createResponse.Size;
                await UpdateControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
                return (true, controlFile);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating named query projection: {S3Key}", parsedNamedQuery.StorageKey);
                return (false, controlFile);
            }
        }
        
        private async Task<ControlFile> CreateControlFile(List<Asset> assets,
            StoredParsedNamedQuery parsedNamedQuery)
        {
            Logger.LogInformation("Creating new control file: {ControlS3Key}", parsedNamedQuery.ControlFileStorageKey);
            var relevantRoles = GetRelevantRoles(assets, parsedNamedQuery);
            var controlFile = new ControlFile
            {
                Created = DateTime.UtcNow,
                Key = parsedNamedQuery.StorageKey,
                Exists = false,
                InProcess = true,
                ItemCount = assets.Count,
                SizeBytes = 0,
                Roles = relevantRoles.IsNullOrEmpty() ? null : relevantRoles,
            };

            await UpdateControlFile(parsedNamedQuery.ControlFileStorageKey, controlFile);
            return controlFile;
        }

        private List<string> GetRelevantRoles(List<Asset> assets, StoredParsedNamedQuery parsedNamedQuery)
        {
            // Only add whitelisted roles that are present in the result collection to the ControlFile as those are
            // the only roles that will be required for access
            var whitelistRoles =
                NamedQuerySettings.CustomerOverrides.TryGetValue(parsedNamedQuery.Customer.ToString(), out var overrides)
                    ? overrides.PdfRolesWhitelist
                    : Enumerable.Empty<string>();

            var distinctRoles = assets.SelectMany(a => a.RolesList).Distinct().ToList();
            var relevantRoles = distinctRoles.Intersect(whitelistRoles).ToList();
            return relevantRoles;
        }

        private Task UpdateControlFile(string controlFileKey, ControlFile? controlFile) =>
            BucketWriter.WriteToBucket(StorageKeyGenerator.GetOutputLocation(controlFileKey),
                JsonConvert.SerializeObject(controlFile), "application/json");

        protected abstract Task<CreateProjectionResult> CreateFile(T parsedNamedQuery, List<Asset> assets,
            CancellationToken cancellationToken);
    }
}