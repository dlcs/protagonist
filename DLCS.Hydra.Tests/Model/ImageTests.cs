using DLCS.HydraModel;
using FluentAssertions;
using Hydra;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DLCS.Hydra.Tests.Model;

public class ImageTests
{
    private const string BaseUrl = "https://www.example.org";
    private const string ImageApiPath = "/customers/1/spaces/1/images/my-image";
    private readonly JsonSerializerSettings serializerSettings;

    public ImageTests()
    {
        serializerSettings = new JsonSerializerSettings();
        serializerSettings.ApplyHydraSerializationSettings();
    }

    [Fact]
    public void ImageClass_Has_ImageId()
    {
        var image = new Image(BaseUrl, 1, 1, "my-image");

        var expected = BaseUrl + ImageApiPath;
        
        image.Id.Should().Be(expected);
    }
    
    
    [Fact]
    public void ImageClass_SerialisesTo_URLForImage_At_Id()
    {
        var expected = BaseUrl + ImageApiPath;
        var image = new Image(BaseUrl, 1, 1, "my-image");
        var jsonString = JsonConvert.SerializeObject(image, serializerSettings);
        var jObject = JObject.Parse(jsonString);
        jObject["@id"].Value<string>().Should().Be(expected);
    }
    
    [Fact]
    public void ImageClass_SerialisesTo_shortStringForImage_Id()
    {
        var expected = "my-image";
        var image = new Image(BaseUrl, 1, 1, expected);
        var jsonString = JsonConvert.SerializeObject(image, serializerSettings);
        var jObject = JObject.Parse(jsonString);
        jObject["id"].Value<string>().Should().Be(expected);
    }
    
    
    [Fact]
    public void ImageClass_Has_SpecificContext()
    {
        var expected = BaseUrl + "/contexts/Image.jsonld";
        var image = new Image(BaseUrl, 1, 1, "my-image");
        var jsonString = JsonConvert.SerializeObject(image, serializerSettings);
        var jObject = JObject.Parse(jsonString);
        jObject["@context"].Value<string>().Should().Be(expected);
    }
    

}