using System;
using System.Collections.Generic;

namespace API.Client.JsonLd
{
    // TODO - as a result of refactoring from two different directions into API.Client, we now have this class AND Image.
    // need to get rid of one of them!

    public class AssetJsonLD  : JsonLdBase
    {
        public override string Type => "Image";
        public DateTime? Created { get; set; }
        public string? Origin { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Roles { get; set; }
        public string? PreservedUri { get; set; }
        public string? String1 { get; set; }
        public string? String2 { get; set; }
        public string? String3 { get; set; }
        public int? MaxUnauthorised { get; set; }
        public long? Number1 { get; set; }
        public long? Number2 { get; set; }
        public long? Number3 { get; set; }
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
                String1 = String1,
                String2 = String2,
                String3 = String3,
                MaxUnauthorised = MaxUnauthorised,
                Number1 = Number1,
                Number2 = Number2,
                Number3 = Number3,
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