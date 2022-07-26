using System.Collections.Generic;
using DLCS.Core.Strings;
using Newtonsoft.Json.Linq;

namespace DLCS.Model.Auth
{
    /// <summary>
    /// Represents a role-provider configuration block, optionally defined per host
    /// </summary>
    /// <remarks>
    /// This is for backwards compat where the payload can be a single configuration element or a dictionary,
    /// keyed by host
    /// </remarks>
    public class RoleProviderConfigBlock
    {
        private const string Default = "default";
        
        public Dictionary<string, RoleProviderConfiguration> Configuration { get; init; }

        /// <summary>
        /// Get <see cref="RoleProviderConfiguration"/> for specified host, or default if host not found.
        /// </summary>
        /// <param name="host">Host to get configuration for</param>
        public RoleProviderConfiguration GetForHost(string host)
            => Configuration.TryGetValue(host, out var config) ? config : Configuration[Default];

        /// <summary>
        /// Parse base64 encoded JSON to <see cref="RoleProviderConfigBlock"/>.
        /// Input can be a single JSON element, or a dictionary of JSON elements.
        /// </summary>
        /// <param name="roleProviderConfiguration">Base64 encoded config</param>
        /// <returns>Parse config block</returns>
        public static RoleProviderConfigBlock FromBase64Json(string roleProviderConfiguration)
        {
            var json = GetRoleProviderJObject(roleProviderConfiguration);

            if (IsSingleConfig(json))
            {
                return new RoleProviderConfigBlock
                {
                    Configuration = new Dictionary<string, RoleProviderConfiguration>
                    {
                        [Default] = json.ToObject<RoleProviderConfiguration>()
                    }
                };
            }

            return new RoleProviderConfigBlock
            {
                Configuration = json.ToObject<Dictionary<string, RoleProviderConfiguration>>()
            };

        }

        private static JObject GetRoleProviderJObject(string roleProviderConfiguration)
        {
            var decodedBlock = roleProviderConfiguration.DecodeBase64();
            JObject json = JObject.Parse(decodedBlock);
            return json;
        }

        // TODO - when is "config" != "cas"
        private static bool IsSingleConfig(JObject json)
            => json["config"] != null && json["config"].Type == JTokenType.String &&
               json["config"].Value<string>() == "cas";
    }
}