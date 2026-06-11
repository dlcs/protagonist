using System;
using Hydra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace DLCS.HydraModel.Converters;

/// <summary>
/// This converter, activated for <see cref="FlexCollection{T}"/>, checks whether the incoming JSON is a single
/// <typeparamref name="T"/> object, a JSON Array of <typeparamref name="T"/> objects, or a HydraCollection where
/// the 1..N <typeparamref name="T"/> objects exist as an array under `member` property
/// </summary>
/// <typeparam name="T"></typeparam>
public class FlexCollectionConverter<T> : JsonConverter<FlexCollection<T>> where T : JsonLdBase
{
    public static void Register(JsonSerializerSettings settings)
    {
        settings.Converters.Add(new FlexCollectionConverter<T>());
    }
    
    public override bool CanWrite => false;

    public override void WriteJson(JsonWriter writer, FlexCollection<T>? value, JsonSerializer serializer)
        => throw new NotSupportedException();

    public override FlexCollection<T>? ReadJson(JsonReader reader, Type objectType, FlexCollection<T>? existingValue,
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
                return new FlexCollection<T>(array ?? []);
            case JTokenType.Object:
                var items = ReadObjectCase((JObject)token, serializer);
                return new FlexCollection<T>(items);
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
        var members = obj["member"];

        if (members == null)
        {
            return [];
        }

        return members.Type != JTokenType.Array 
            ? throw new JsonSerializationException("'member' must be an array.") 
            : members.ToObject<T[]>(serializer) ?? [];
    }

}
