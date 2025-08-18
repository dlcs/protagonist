using System.Collections.Generic;
using DLCS.Repository.Serialisation;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DLCS.Repository.Tests.Serialisation;

public class SessionUserRoleSerialiserTests
{
    private readonly JsonSerializerSettings jsonSerializerSettings;
    public SessionUserRoleSerialiserTests()
    {
        jsonSerializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new SessionUserRoleSerialiser() }
        };
    }

    [Fact]
    public void WriteJson_ReturnsExpected()
    {
        // Arrange
        var roles = new Dictionary<int, List<string>>
        {
            [1] = new() { "foo" },
            [10] = new() { "foo", "bar" }
        };

        var expected =
            "[{\"customer\":1,\"customerRoles\":[\"foo\"]},{\"customer\":10,\"customerRoles\":[\"foo\",\"bar\"]}]";
        
        // Act
        var result = JsonConvert.SerializeObject(roles, jsonSerializerSettings);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void ReadJson_ReturnsExpected()
    {
        // Arrange
        var json =
            "[{\"customer\":1,\"customerRoles\":[\"foo\"]},{\"customer\":10,\"customerRoles\":[\"foo\",\"bar\"]}]";
        var expected = new Dictionary<int, List<string>>
        {
            [1] = new() { "foo" },
            [10] = new() { "foo", "bar" }
        };

        // Act
        var result = JsonConvert.DeserializeObject<Dictionary<int, List<string>>>(json, jsonSerializerSettings);
        
        // Assert
        result.Should().BeEquivalentTo(expected);
    }
}