using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DLCS.Core.Enum;

public class FlagConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, System.Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return default;
            case JsonTokenType.StartArray:
                var flags = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    var flag = reader.GetString();

                    if (flag != null)
                    {
                        flags.Add(flag);
                    }
                }
                
                TEnum ret = flags.ToEnumFlags<TEnum>();
                return ret;
            default:
                throw new JsonException();
        }
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var result = new List<string>();

        foreach (TEnum enumValue in System.Enum.GetValues(typeof(TEnum)))
        {
            if (value.HasFlag(enumValue))
            {
                result.Add(enumValue.GetDescription()!);
            }
        }
        
        writer.WriteRawValue(JsonSerializer.Serialize(result));
    }
}
