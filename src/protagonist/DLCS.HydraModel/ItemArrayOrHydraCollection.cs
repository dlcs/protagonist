using DLCS.HydraModel.Converters;
using Hydra;
using Hydra.Collections;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

/// <summary>
/// Used as a wrapper/guide for deserializer to accept either <typeparamref name="T"/>, or plain JSON array of
/// <typeparamref name="T"/>, or a <see cref="HydraCollection{T}"/>
/// </summary>
/// <param name="items">Used by deserializing converter to set item or items extracted from JSON payload</param>
/// <typeparam name="T">Any valid <see cref="JsonLdBase"/> derrivative</typeparam>
public class ItemArrayOrHydraCollection<T>(params T[] items)
    where T : JsonLdBase
{
    public T[] Items { get; } = items;
}
