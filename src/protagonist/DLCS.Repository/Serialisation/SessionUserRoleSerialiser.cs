using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DLCS.Repository.Serialisation;

/// <summary>
/// Custom serializer to handle SessionUser.Roles property
/// </summary>
/// <remarks>This can be deleted when we don't need compatibility with any data written by Deliverator</remarks>
public class SessionUserRoleSerialiser : JsonConverter<Dictionary<int, List<string>>>
{
    public override void WriteJson(JsonWriter writer, Dictionary<int, List<string>>? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var (customer, roles) in value)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("customer");
            writer.WriteValue(customer);
            writer.WritePropertyName("customerRoles");
            writer.WriteStartArray();
            foreach (var role in roles)
            {
                writer.WriteValue(role);
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    public override Dictionary<int, List<string>>? ReadJson(JsonReader reader, Type objectType,
        Dictionary<int, List<string>>? existingValue, bool hasExistingValue,
        JsonSerializer serializer)
    {
        existingValue ??= new Dictionary<int, List<string>>();
        foreach (JToken roleToken in JArray.Load(reader))
        {
            var customer = roleToken["customer"].Value<int>();
            var customerRoles =
                roleToken["customerRoles"]
                    .Value<JArray>()
                    .Select(customerRole => customerRole.Value<string>())
                    .ToList();
            existingValue[customer] = customerRoles;
        }

        return existingValue;
    }
}