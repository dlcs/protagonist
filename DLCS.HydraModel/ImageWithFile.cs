namespace DLCS.HydraModel
{
    public class ImageWithFile : Image
    {
        public ImageWithFile(string baseUrl, int customerId, int space, string modelId) : base(baseUrl, customerId, space, modelId)
        {
        }
        
        public byte[]? File { get; set; }
        
        public Image ToImage() => new Image(BaseUrl, CustomerId, Space, ModelId!)
        {
            StorageIdentifier = StorageIdentifier,
            Created = Created,
            Origin = Origin,
            InitialOrigin = InitialOrigin,
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
            Family = Family,
            MediaType = MediaType,
            Text = Text,
            TextType = TextType
        };
    }
}