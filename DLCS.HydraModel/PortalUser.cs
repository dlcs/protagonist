using System;
using DLCS.HydraModel.Settings;
using Hydra;
using Hydra.Model;
using Newtonsoft.Json;

namespace DLCS.HydraModel
{
    [HydraClass(typeof(PortalUserClass),
        Description = "A user of the portal. Represents an account for use by a person, rather than by a machine. You can create " +
                      "as many portal user accounts as required. Note that the roles a portal user has relate to DLCS permissions " +
                      "rather than permissions on your image resources.",
        UriTemplate = "/customers/{0}/portalUsers/{1}")]
    public class PortalUser : DlcsResource
    {
        [JsonIgnore]
        public string ModelId { get; set; }
        [JsonIgnore]
        public int CustomerId { get; set; }

        public PortalUser() { }

        public PortalUser(HydraSettings settings, int customerId, string userId, string email, DateTime created, bool enabled)
        {
            CustomerId = customerId;
            ModelId = userId;
            Email = email;
            Created = created;
            Enabled = enabled;
            Init(settings, true, customerId, ModelId);
        }


        [RdfProperty(Description = "The email address",
            Range = Names.XmlSchema.String, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 11, PropertyName = "email")]
        public string Email { get; set; }

        [RdfProperty(Description = "Create date",
            Range = Names.XmlSchema.DateTime, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 12, PropertyName = "created")]
        public DateTime Created { get; set; }

        [HydraLink(Description = "Collection of Role resources that the user has. These roles should not" +
                                   " be confused with the roles associated with images and authservices, which govern the interactions that" +
                                   " end users can have with your image resources. These PortalUser roles govern the actions that your handful" +
                                   " of registered DLCS back end users can perform in the portal. ",
            Range = Names.Hydra.Collection, ReadOnly = true, WriteOnly = false)]
        [JsonProperty(Order = 13, PropertyName = "roles")]
        public string Roles { get; set; }

        [RdfProperty(Description = "Whether the user can log in - for temporary or permanent rescinding of access.",
            Range = Names.XmlSchema.Boolean, ReadOnly = false, WriteOnly = false)]
        [JsonProperty(Order = 14, PropertyName = "enabled")]
        public bool Enabled { get; set; }
    }

    public class PortalUserClass : Class
    {
        public PortalUserClass()
        {
            BootstrapViaReflection(typeof (PortalUser));
        }

        public override void DefineOperations()
        {
            SupportedOperations = CommonOperations.GetStandardResourceOperations(
                "_:customer_portalUser_", "Portal User", Id, 
                "GET", "PUT", "PATCH", "DELETE");


            GetHydraLinkProperty("roles").SupportedOperations = CommonOperations
                .GetStandardCollectionOperations("_:customer_portalUser_portalRole_", "Portal Role", "vocab:PortalRole");
        }
    }
}
