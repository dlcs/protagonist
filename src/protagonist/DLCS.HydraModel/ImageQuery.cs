using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DLCS.HydraModel;

public class ImageQuery
{
    private static readonly JsonSerializerSettings JsonSerializerSettings;

    static ImageQuery()
    {
        JsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
    }
    
    public int? Space { get; set; }

    public string? String1 { get; set; }
    public string? String2 { get; set; }
    public string? String3 { get; set; }

    public int? Number1 { get; set; }
    public int? Number2 { get; set; }
    public int? Number3 { get; set; }

    public static ImageQuery? Parse(string s)
    {
        try
        {
            return JsonConvert.DeserializeObject<ImageQuery>(s, JsonSerializerSettings);
        }
        catch
        {
            return null;
        }
    }

    public string ToQueryParam()
    {
        return JsonConvert.SerializeObject(this, JsonSerializerSettings);
    }
}