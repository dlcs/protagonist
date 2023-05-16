using Newtonsoft.Json;

namespace Hydra;

public static class SerializationSettingsX
{
    private const string Iso8601DateFormatString = "O";
        
    public static void ApplyHydraSerializationSettings(this JsonSerializerSettings jsonSettings)
    {
        jsonSettings.DateFormatString = Iso8601DateFormatString;
        jsonSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
        jsonSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        jsonSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
        jsonSettings.Formatting = Formatting.Indented;
        jsonSettings.NullValueHandling = NullValueHandling.Ignore;
    }
}