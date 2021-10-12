using DLCS.HydraModel.Settings;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof (PortalRoleClass),
        Description = "A role that can be assigned to a user of the DLCS portal (not an end user) for the customer to allow control over permissions.",
        UriTemplate = "/portalRoles/{0}")]
    [Unstable(Note = "Under consideration.")]
    public class PortalRole : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }

        public PortalRole() { }

        public PortalRole(HydraSettings settings, string portalRoleId, string name)
        {
            ModelId = portalRoleId;
            Name = name;
            Init(settings, true, portalRoleId);
        }

        [RdfProperty(Description = "The human readable name of the origin strategy",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "name")]
        public string Name { get; set; }

    }

    public class PortalRoleClass : Class
    {
        public PortalRoleClass()
        {
            BootstrapViaReflection(typeof (PortalRole));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:portalRole_", "Portal Role", Id,
                "GET");

        }
    }
}