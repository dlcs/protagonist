using DLCS.HydraModel.Converters;
using Hydra;
using Hydra.Collections;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

/// <summary>
/// With deserialization step in <see cref="FlexCollectionConverter{T}"/>, this collection allows for
/// converting JSON input that's either a JSON representation of a single object <typeparamref name="T"/>, or plain JSON array of
/// <typeparamref name="T"/>, or a <see cref="HydraCollection{T}"/>
/// </summary>
/// <param name="items">Used by deserializing converter to set item or items extracted from JSON payload</param>
/// <typeparam name="T">Any valid <see cref="JsonLdBase"/> derrivative</typeparam>
public class FlexCollection<T>(params T[] items)
    where T : JsonLdBase
{
    public T[] Items { get; } = items;
}
