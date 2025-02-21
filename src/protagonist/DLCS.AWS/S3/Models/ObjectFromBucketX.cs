using DLCS.Core.Streams;
using Newtonsoft.Json;

namespace DLCS.AWS.S3.Models;

public static class ObjectFromBucketX
{
    private static readonly JsonSerializer Serializer = new();
        
    /// <summary>
    /// Deserialize objectFromBucket stream to specified type.
    /// </summary>
    /// <param name="objectFromBucket"><see cref="ObjectFromBucket"/> containing stream to deserialize</param>
    /// <typeparam name="T">Target type</typeparam>
    /// <returns>Deserialized object, or null of not found</returns>
    public static async Task<T?> DeserializeFromJson<T>(this ObjectFromBucket objectFromBucket)
        where T : class
    {
        await using var stream = objectFromBucket.Stream;
        if (stream.IsNull())
        {
            return null;
        }

        using var sr = new StreamReader(stream!);
        await using var jsonTextReader = new JsonTextReader(sr);
        var objects = Serializer.Deserialize<T>(jsonTextReader);
        return objects;
    }
}