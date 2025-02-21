using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.AWS.S3;
using DLCS.AWS.S3.Models;
using DLCS.Model.Assets.NamedQueries;
using DLCS.Repository.NamedQueries.Models;

namespace DLCS.Repository.NamedQueries;

/// <summary>
/// Class that handles interacting with NamedQuery projection + control files in backing store
/// </summary>
public class NamedQueryStorageService
{
    private readonly IBucketReader bucketReader;
    private readonly IBucketWriter bucketWriter;
    private readonly IStorageKeyGenerator storageKeyGenerator;

    public NamedQueryStorageService(
        IBucketReader bucketReader,
        IStorageKeyGenerator storageKeyGenerator, 
        IBucketWriter bucketWriter)
    {
        this.bucketReader = bucketReader;
        this.storageKeyGenerator = storageKeyGenerator;
        this.bucketWriter = bucketWriter;
    }
    
    /// <summary>
    /// Get <see cref="ControlFile"/> stored for parsed named query.
    /// </summary>
    public async Task<ControlFile?> GetControlFile(StoredParsedNamedQuery parsedNamedQuery, 
        CancellationToken cancellationToken)
    {
        var controlObject = await LoadStoredObject(parsedNamedQuery.ControlFileStorageKey, cancellationToken);
        if (controlObject.Stream == Stream.Null) return null;
        return await controlObject.DeserializeFromJson<ControlFile>();
    }

    /// <summary>
    /// Get typed <see cref="ControlFile"/> stored for parsed named query.
    /// </summary>
    public async Task<T?> GetControlFile<T>(StoredParsedNamedQuery parsedNamedQuery,
        CancellationToken cancellationToken)
        where T : ControlFile
    {
        var controlObject = await LoadStoredObject(parsedNamedQuery.ControlFileStorageKey, cancellationToken);
        if (controlObject.Stream == Stream.Null) return null;
        return await controlObject.DeserializeFromJson<T>();
    }

    /// <summary>
    /// Get an <see cref="ObjectFromBucket"/> for stored results of NQ
    /// </summary>
    public async Task<ObjectFromBucket> LoadProjection(StoredParsedNamedQuery parsedNamedQuery, 
        CancellationToken cancellationToken)
    {
        var storedObject = await LoadStoredObject(parsedNamedQuery.StorageKey, cancellationToken);
        return storedObject;
    }

    /// <summary>
    /// Delete stored resources for specified NamedQuery.
    /// This is both the control-file and projection 
    /// </summary>
    public async Task<bool> DeleteStoredNamedQuery(StoredParsedNamedQuery parsedNamedQuery)
    {
        var controlFile = GetStorageLocation(parsedNamedQuery.ControlFileStorageKey);
        var projection = GetStorageLocation(parsedNamedQuery.StorageKey);

        try
        {
            await bucketWriter.DeleteFromBucket(controlFile, projection);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    private Task<ObjectFromBucket> LoadStoredObject(string key, CancellationToken cancellationToken)
    {
        var outputLocation = GetStorageLocation(key);
        return bucketReader.GetObjectFromBucket(outputLocation, cancellationToken);
    }

    private ObjectInBucket GetStorageLocation(string key) => storageKeyGenerator.GetOutputLocation(key);
}