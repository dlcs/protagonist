namespace Engine.Ingest.Timebased.Transcode;

/// <summary>
/// Implementation of <see cref="IMediaTranscoder"/> using AWS Elemental MediaConvert for transcoding
/// </summary>
public class MediaConvert : IMediaTranscoder
{
    public Task<bool> InitiateTranscodeOperation(IngestionContext context, Dictionary<string, string> jobMetadata, CancellationToken token = default)
    {
        // Get the queue arn from the name
        
        // Build up a list of outputs, based on the delivery channel policies / presets etc
        
        // Append jobMetadata
        
        // Call wrapper.CreateJob (can we rationalise this)
        
        // Handle the CreateJob output (it's the underlying model though)
        
        // Call .PersistJobId
        
        throw new NotImplementedException();
    }
}
