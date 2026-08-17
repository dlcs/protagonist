using API.Infrastructure.Swagger;
using DLCS.HydraModel;
using Hydra;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Newtonsoft;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace API.Tests.Infrastructure;

public class HydraReadWriteSchemaFilterTests
{
    private readonly SchemaGenerator schemaGenerator;

    public HydraReadWriteSchemaFilterTests()
    {
        var serializerSettings = new JsonSerializerSettings();
        serializerSettings.ApplyHydraSerializationSettings();

        schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions { SchemaFilters = { new HydraReadWriteSchemaFilter() } },
            new NewtonsoftDataContractResolver(serializerSettings));
    }

    [Theory]
    [InlineData("created")]
    [InlineData("finished")]
    [InlineData("width")]
    public void GenerateSchema_MarksHydraReadOnlyProperty_AsReadOnly(string property)
    {
        var schema = GenerateSchema<Image>();

        schema.Properties[property].ReadOnly.Should().BeTrue();
        schema.Properties[property].WriteOnly.Should().BeFalse();
    }

    [Fact]
    public void GenerateSchema_LeavesWritableProperty_Unmarked()
    {
        var schema = GenerateSchema<Image>();

        schema.Properties["origin"].ReadOnly.Should().BeFalse();
        schema.Properties["origin"].WriteOnly.Should().BeFalse();
    }

    [Fact]
    public void GenerateSchema_MarksHydraWriteOnlyProperty_AsWriteOnly()
    {
        var schema = GenerateSchema<PortalUser>();

        schema.Properties["password"].WriteOnly.Should().BeTrue();
        schema.Properties["password"].ReadOnly.Should().BeFalse();
    }

    [Fact]
    public void GenerateSchema_MarksReadOnlyPropertyWithReferencedSchema_ViaAllOf()
    {
        // a $ref sibling keyword is ignored by OpenAPI readers, so the reference is wrapped in an allOf
        var schema = GenerateSchema<ResourceWithReferencedProperty>();

        var family = schema.Properties["family"];
        family.ReadOnly.Should().BeTrue();
        family.Reference.Should().BeNull();
        family.AllOf.Should().ContainSingle().Which.Reference.Should().NotBeNull();
    }

    private OpenApiSchema GenerateSchema<T>()
    {
        var repository = new SchemaRepository();
        schemaGenerator.GenerateSchema(typeof(T), repository);
        return repository.Schemas[typeof(T).Name];
    }

    /// <summary>
    /// No Hydra model currently has a read-only property that generates a referenced schema (ie one of a complex
    /// type or enum), so this stands in for one.
    /// </summary>
    public class ResourceWithReferencedProperty
    {
        [RdfProperty(Description = "Asset family", ReadOnly = true, WriteOnly = false)]
        [JsonProperty(PropertyName = "family")]
        public AssetFamily? Family { get; set; }
    }
}
