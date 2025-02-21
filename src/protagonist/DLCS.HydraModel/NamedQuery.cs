using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel;

[HydraClass(typeof(NamedQueryClass),
    Description = "A named query is a URI pattern available on dlcs.io (i.e., not this API) that will return a IIIF resource " +
                  "such as a collection, or manifest, or sequence, or canvas. For example:" +
                  "\n\n```\n" +
                  "https://dlcs.io/resources/iiifly/manifest/43/ae678999\n" +
                  "```\n\n" +
                  "This query is is an instance of the following template:" +
                  "\n\n```\n" +
                  "https://dlcs.io/resources/{customer}/{named-query}/{space}/{string1}\n" +
                  "```\n\n" +
                  "This customer (iiifly) has a named query called 'manifest' that takes two parameters - the space" +
                  " and the string1 metadata field. The query is internally defined to use an additional field - number1 - " +
                  " and to generate a manifest with one sequence, with each canvas in the sequence having one image. The images" +
                  " selected by the query must all have string1=ae678999 in this case, and are ordered by number1." +
                  "  An image query against the dlcs API returns" +
                  " a collection of DLCS Image objects. a Named Query uses an DLCS image query but then projects these images and " +
                  " constructs a IIIF resource from them, using the parameters provided. Information on designing and configuring named queries is" +
                  " provided in a special topic.",
    UriTemplate = "/customers/{0}/namedQueries/{1}")]
[Unstable(Note = "Currently the named query implementation is a placeholder,")]
public class NamedQuery : DlcsResource
{
    [JsonIgnore]
    public string? ModelId { get; set; }
    
    [JsonIgnore]
    public int CustomerId { get; set; }

    public NamedQuery()
    {
    }
    
    public NamedQuery(string baseUrl, int customerId, string modelId)
    {
        CustomerId = customerId;
        ModelId = modelId;
        Init(baseUrl, true, customerId, ModelId);
    }


    [RdfProperty(Description = "The name that appears for the query in the path on https://dlcs.io, e.g., 'manifest'",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 10, PropertyName = "name")]
    public string? Name { get; set; }

    [RdfProperty(Description = "The named query is available to all customers",
        Range = Names.XmlSchema.Boolean, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "global")]
    public bool? Global { get; set; }

    [RdfProperty(Description = "URI template",
        Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
    [JsonProperty(Order = 11, PropertyName = "template")]
    public string? Template { get; set; }

}

public class NamedQueryClass : Class
{
    public NamedQueryClass()
    {
        BootstrapViaReflection(typeof(NamedQuery));
    }

    public override void DefineOperations()
    {
        SupportedOperations = CommonOperations.GetStandardResourceOperations(
            "_:customer_namedqueries_", "Named Query", Id,
            "GET", "PUT", "PATCH", "DELETE");
    }
}
