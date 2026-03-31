using System;
using Hydra;
using Hydra.Collections;
using Newtonsoft.Json;

namespace DLCS.HydraModel.Converters;

public class ItemArrayOrHydraCollectionConverter<T> : JsonConverter<ItemArrayOrHydraCollection<T>> where T : JsonLdBase
{
    public static void Register(JsonSerializerSettings settings)
    {
        settings.Converters.Add(new ItemArrayOrHydraCollectionConverter<T>());
    }
    
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, ItemArrayOrHydraCollection<T>? value, JsonSerializer serializer)
        => throw new NotSupportedException();

    public override ItemArrayOrHydraCollection<T>? ReadJson(JsonReader reader, Type objectType, ItemArrayOrHydraCollection<T>? existingValue,
        bool hasExistingValue, JsonSerializer serializer)
    {
        reader.Read();
        switch (reader.TokenType)
        {
            case JsonToken.String:
                var single = serializer.Deserialize<T>(reader);
                return single == null ? null : new ItemArrayOrHydraCollection<T>(single);
            case JsonToken.StartArray:
                var array = serializer.Deserialize<T[]>(reader);
                return array == null ? null : new ItemArrayOrHydraCollection<T>(array);
            case JsonToken.StartObject:
                var hydra = serializer.Deserialize<HydraCollection<T>>(reader);
                return hydra is not { Members: { } members } ? null : new ItemArrayOrHydraCollection<T>(members);
            default:
                return null;
        }
    }
}
