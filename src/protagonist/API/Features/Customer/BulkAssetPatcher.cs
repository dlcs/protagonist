using System.Collections.Generic;
using API.Infrastructure.Requests;
using DLCS.Core.Collections;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using DLCS.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace API.Features.Customer;

public interface IBulkAssetPatcher
{
    /// <summary>
    /// Updates assets with new values from bulk asset patch requests
    /// </summary>
    public Task<List<Asset>> UpdateAssets(List<AssetId> assetIds, List<string>? value, OperationType operation,
        string field, int customerId, CancellationToken cancellationToken = default);
}

public class BulkAssetPatcher(DlcsContext dlcsContext) : IBulkAssetPatcher
{
    public async Task<List<Asset>> UpdateAssets(List<AssetId> assetIds, List<string>? value, OperationType operation, 
        string field, int customerId, CancellationToken cancellationToken = default)
    {
        switch (field)
        {
            case SupportedFields.ManifestField:
                await UpdateManifests(assetIds, value, operation, customerId, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field '{field}'");
        };

        var updatedAssets = await dlcsContext.Images.AsNoTracking()
            .Where(i => i.Customer == customerId && assetIds.Contains(i.Id))
            .ToListAsync(cancellationToken);
        
        return updatedAssets;
    }

    private async Task UpdateManifests(List<AssetId> assetIds, List<string>? value, OperationType operation, int customerId,
        CancellationToken cancellationToken)
    {
        if (value.IsEmpty()) value = null;
        
        switch (operation)
        {
            case OperationType.Add:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id) && a.Customer == customerId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(a => a.Manifests, a => a.Manifests.Concat(value)), cancellationToken);
                break;
            case OperationType.Remove:
                await RemoveManifests(assetIds, value, customerId, cancellationToken);
                break;
            case OperationType.Replace:
                await dlcsContext.Images
                    .Where(a => assetIds.Any(aid => aid == a.Id) && a.Customer == customerId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Manifests, value),
                        cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported operation '{operation}'");
        }
    }

    // allows a query to be generated using a list, while avoiding issues with SQL injection
    private async Task RemoveManifests(List<AssetId> assetIds, List<string>? value, int customerId, CancellationToken cancellationToken)
    {
        await using var batchedQuery = new NpgsqlBatch(await dlcsContext.GetOpenNpgSqlConnection());
        
        foreach (var valueToRemove in value ?? [])
        {
            var parameters = assetIds.Select((id, index) =>
                    // index + 2 is due to one-based index for positional parameters + valueToRemove parameter being set in the $1
                    new KeyValuePair<int,NpgsqlParameter>(index + 2, new NpgsqlParameter { Value = id.ToString() }))
                .ToList();
            var parameterNames = string.Join(", ", parameters.Select(p => $"${p.Key}"));
            parameters.Add(new KeyValuePair<int, NpgsqlParameter>(1, new NpgsqlParameter {Value = valueToRemove}));
            // customer is last in the query and the index is +2, so the index of the last value becomes count + 1
            var customerIndex = parameters.Count + 1;
            parameters.Add(new KeyValuePair<int, NpgsqlParameter>(customerIndex, new NpgsqlParameter {Value = customerId}));

            var command = new NpgsqlBatchCommand(
                $"update \"Images\" set \"Manifests\" = array_remove(\"Manifests\", $1) where \"Id\" in ({parameterNames}) and \"Customer\" = ${customerIndex}");
            var orderedParameters = parameters.OrderBy(x => x.Key);
            command.Parameters.AddRange(orderedParameters.Select(x => x.Value).ToArray());
            batchedQuery.BatchCommands.Add(command);
        }
        
        await batchedQuery.ExecuteNonQueryAsync(cancellationToken);
    }

    public class SupportedFields
    {
        public const string ManifestField = "manifests";
    }
}
