using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DLCS.HydraModel.Converters;

public class ImageDeliveryChannelsConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        List<DeliveryChannel> deliveryChannels = new();
        if (reader.TokenType == JsonToken.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    break;
                }
                if (reader.TokenType == JsonToken.StartObject)
                {
                    var newDeliveryChannel = serializer.Deserialize<DeliveryChannel>(reader);
                    deliveryChannels.Add(newDeliveryChannel);
                }
                if (reader.TokenType == JsonToken.String)
                {
                    var channel = serializer.Deserialize<string>(reader);
                    var newDeliveryChannel = new DeliveryChannel()
                    {
                        Channel = channel
                    };
                    
                    deliveryChannels.Add(newDeliveryChannel);
                } 
            }
        }
        
        return deliveryChannels.ToArray();
    }
    
    public override bool CanConvert(Type objectType)
    {
        return true;
    }
}