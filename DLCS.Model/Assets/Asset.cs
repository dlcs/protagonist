using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using DLCS.Core.Collections;

namespace DLCS.Model.Assets
{
    /// <summary>
    /// Represents an Asset that is stored in the DLCS database.
    /// </summary>
    public class Asset
    {
        public string Id { get; set; }
        public int Customer { get; set; }
        public int Space { get; set; }
        public DateTime? Created { get; set; }
        public string? Origin { get; set; }
        public string? Tags { get; set; }
        public string? Roles { get; set; }
        public string? PreservedUri { get; set; }
        public string? Reference1 { get; set; }
        public string? Reference2 { get; set; }
        public string? Reference3 { get; set; }
        public int? NumberReference1 { get; set; }
        public int? NumberReference2 { get; set; }
        public int? NumberReference3 { get; set; }
        
        // -1 = null (all open), 0 = no allowed size without being auth
        public int? MaxUnauthorised { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? Error { get; set; }
        public int? Batch { get; set; }
        public DateTime? Finished { get; set; }
        public bool? Ingesting { get; set; }
        public string? ImageOptimisationPolicy { get; set; }
        public string? ThumbnailPolicy { get; set; }
        public AssetFamily? Family { get; set; }
        public string? MediaType { get; set; }
        public long? Duration { get; set; }

        private IEnumerable<string>? rolesList;
        
        // TODO - map this via Dapper on way out of DB?
        [NotMapped]
        public IEnumerable<string> RolesList
        {
            get
            {
                if (rolesList == null && !string.IsNullOrEmpty(Roles))
                {
                    rolesList = Roles.Split(",", StringSplitOptions.RemoveEmptyEntries); 
                }

                return rolesList ??= Enumerable.Empty<string>();
            }
            set => Roles = value.IsNullOrEmpty() ? String.Empty : String.Join(',', value);
        }
        
        private IEnumerable<string>? tagsList;
        
        [NotMapped]
        public IEnumerable<string> TagsList
        {
            get
            {
                if (tagsList == null && !string.IsNullOrEmpty(Tags))
                {
                    tagsList = Tags.Split(",", StringSplitOptions.RemoveEmptyEntries); 
                }

                return tagsList ??= Enumerable.Empty<string>();
            }
            set => Tags = value.IsNullOrEmpty() ? String.Empty : String.Join(',', value);
        }
        
        public bool RequiresAuth => !string.IsNullOrWhiteSpace(Roles) && MaxUnauthorised >= 0;
        
        // TODO - how to handle this? Split model + entity?
        public string? InitialOrigin { get; set; }
        
        /// <summary>
        /// Get origin to use for ingestion. This will be 'initialOrigin' if present, else origin.
        /// </summary>
        public string GetIngestOrigin()
            => string.IsNullOrWhiteSpace(InitialOrigin) ? Origin : InitialOrigin;
        
        private string uniqueName;
        /// <summary>
        /// Get the identifier part from from Id.
        /// Id contains {cust}/{space}/{identifier}
        /// </summary>
        /// <returns></returns>
        public string GetUniqueName()
        {
            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                uniqueName = Id[(Id.LastIndexOf('/') + 1)..];
            }

            return uniqueName;
        }

        public Asset ShallowCopy()
        {
            return (Asset) this.MemberwiseClone();
        }
    }
}
