using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace API.Features.Image.Models
{
    // NOTE - these classes will be worked on when 'real' implementation of API is added. These are passed directly
    // to API for now.
    // TODO - Ideally we could inherit from DLCS.Model.Assets.Asset here but would need to make all props nullable
    // and I'm unsure of side effects

    public class AssetJsonLD 
    {
        [JsonProperty("@context")]
        public string? Context { get; set; }
        
        [JsonProperty("@type")]
        public string? Type { get; set; }
        
        [JsonProperty("@id")]
        public string? Id { get; set; }
        public DateTime? Created { get; set; }
        public string? Origin { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Roles { get; set; }
        public string? PreservedUri { get; set; }
        public string? Reference1 { get; set; }
        public string? Reference2 { get; set; }
        public string? Reference3 { get; set; }
        public int? MaxUnauthorised { get; set; }
        public long? NumberReference1 { get; set; }
        public long? NumberReference2 { get; set; }
        public long? NumberReference3 { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public long? Duration { get; set; }
        public string? Error { get; set; }
        public int? Batch { get; set; }
        public DateTime? Finished { get; set; }
        public bool? Ingesting { get; set; }
        public string? ImageOptimisationPolicy { get; set; }
        public string? ThumbnailPolicy { get; set; }
        public string? InitialOrigin { get; set; }
        public char? Family { get; set; }
        public string? MediaType { get; set; }
    }
    
    public class AssetJsonLdWithBytes : AssetJsonLD
    {
        public byte[] File { get; set; }

        public AssetJsonLD ToImageJsonLD() =>
            new AssetJsonLD
            {
                Type = Type,
                Id = Id,
                Created = Created,
                Origin = Origin,
                Tags = Tags,
                Roles = Roles,
                PreservedUri = PreservedUri,
                Reference1 = Reference1,
                Reference2 = Reference2,
                Reference3 = Reference3,
                MaxUnauthorised = MaxUnauthorised,
                NumberReference1 = NumberReference1,
                NumberReference2 = NumberReference2,
                NumberReference3 = NumberReference3,
                Width = Width,
                Height = Height,
                Duration = Duration,
                Error = Error,
                Batch = Batch,
                Finished = Finished,
                Ingesting = Ingesting,
                ImageOptimisationPolicy = ImageOptimisationPolicy,
                ThumbnailPolicy = ThumbnailPolicy,
                InitialOrigin = InitialOrigin,
                Family = Family,
                MediaType = MediaType,
            };
    }
}