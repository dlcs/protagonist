using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using DLCS.Core.Collections;
using Hydra;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace API.Infrastructure.Swagger;

/// <summary>
/// Schema filter that maps the Hydra <see cref="SupportedPropertyAttribute"/> ReadOnly/WriteOnly flags onto the
/// equivalent OpenAPI schema keywords.
/// </summary>
/// <remarks>
/// OpenAPI treats <c>readOnly</c> as "only appears in responses" and <c>writeOnly</c> as "only appears in requests",
/// so tooling (including Swagger UI) omits read-only properties from PUT/POST/PATCH request bodies and write-only
/// properties from GET responses.
/// </remarks>
public class HydraReadWriteSchemaFilter : ISchemaFilter
{
    private readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, SupportedPropertyAttribute>> cache = new();

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties.IsNullOrEmpty()) return;

        var hydraProperties = cache.GetOrAdd(context.Type, GetHydraProperties);
        if (hydraProperties.Count == 0) return;

        // iterate a snapshot of the keys as EnsureNotReference can replace entries in schema.Properties
        foreach (var name in schema.Properties.Keys.ToList())
        {
            if (!hydraProperties.TryGetValue(name, out var hydraProperty)) continue;
            if (hydraProperty is { ReadOnly: false, WriteOnly: false }) continue;

            var target = EnsureNotReference(schema, name, schema.Properties[name]);
            target.ReadOnly = hydraProperty.ReadOnly;
            target.WriteOnly = hydraProperty.WriteOnly;
        }
    }

    /// <summary>
    /// A "$ref" sibling keyword is ignored by OpenAPI readers, so wrap referenced schemas (for complex types/enums)
    /// in an allOf that the readOnly/writeOnly flag can be set on.
    /// </summary>
    /// <remarks>
    /// Default: "family": {"$ref": "#/components/schemas/AssetFamily","readOnly": true }
    /// With this: "family": {"allOf": [ { "$ref": "#/components/schemas/AssetFamily" } ],"readOnly": true }
    /// </remarks>
    private static OpenApiSchema EnsureNotReference(OpenApiSchema parent, string name, OpenApiSchema propertySchema)
    {
        if (propertySchema.Reference == null) return propertySchema;

        var wrapper = new OpenApiSchema { AllOf = new List<OpenApiSchema> { propertySchema } };
        parent.Properties[name] = wrapper;
        return wrapper;
    }

    private static IReadOnlyDictionary<string, SupportedPropertyAttribute> GetHydraProperties(Type type)
    {
        var hydraProperties = new Dictionary<string, SupportedPropertyAttribute>(StringComparer.OrdinalIgnoreCase);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = property.GetCustomAttribute<SupportedPropertyAttribute>(true);
            if (attribute == null) continue;

            // the schema is keyed on the serialised name, which JsonProperty can override
            var name = property.GetCustomAttribute<JsonPropertyAttribute>(true)?.PropertyName ?? property.Name;
            hydraProperties[name] = attribute;
        }

        return hydraProperties;
    }
}
