namespace API.Client.OldJsonLd
{
    public class ImageWithFile : Image
    {
        public byte[] File { get; set; }

        public Image ToImage() => new Image
        {
            ModelId = ModelId,
            Created = Created,
            Origin = Origin,
            Tags = Tags,
            Roles = Roles,
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
            MediaType = MediaType
        };
    }
}