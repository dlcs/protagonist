using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DLCS.HydraModel.Converters;

public class ImageDeliveryChannelsConverter : JsonConverter<DeliveryChannel[]>
{
    public override void WriteJson(JsonWriter writer, DeliveryChannel[]? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value); // Serialize the values normally
    }

    public override DeliveryChannel[]? ReadJson(JsonReader reader, Type objectType, DeliveryChannel[]? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
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
                
                // If an object is found, deserialize it as a hydra delivery channel
                if (reader.TokenType == JsonToken.StartObject)
                {
                    var newDeliveryChannel = serializer.Deserialize<DeliveryChannel>(reader);
                    if (newDeliveryChannel == null) continue;
                    deliveryChannels.Add(newDeliveryChannel);
                }
                
                // Otherwise, if a string is found it should be used as the channel for a new hydra delivery channel
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
}