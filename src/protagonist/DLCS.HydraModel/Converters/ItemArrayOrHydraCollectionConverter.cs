using System;
using System.Text.Json;
using Hydra;
using Hydra.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

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
        // Sanity check -> fail fast
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        // Buffer the whole current JSON value - we will need it to differentiate HydraCollection<T> from just T
        var token = JToken.Load(reader);
        
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault - we only care about array/object
        switch (token.Type)
        {
            case JTokenType.Array:
                var array = token.ToObject<T[]>(serializer);
                return new ItemArrayOrHydraCollection<T>(array ?? []);
            case JTokenType.Object:
                var items = ReadObjectCase((JObject)token, serializer);
                return new ItemArrayOrHydraCollection<T>(items);
            default:
                return null;
        }
    }
    
    private static T[] ReadObjectCase(JObject obj, JsonSerializer serializer)
    {
        if ((string?)obj["@type"] != "Collection")
        {
            // Not HydraCollection - most likely a single object provided
            var single = obj.ToObject<T>(serializer);
            return single == null ? [] : [single];
        }

        // Seems to be HydraCollection
        var members = obj["members"];

        if (members == null)
        {
            return [];
        }

        return members.Type != JTokenType.Array 
            ? throw new JsonSerializationException("'members' must be an array.") 
            : members.ToObject<T[]>(serializer) ?? [];
    }

}
