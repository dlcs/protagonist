using System;
using Newtonsoft.Json;

namespace DLCS.Model.Assets.Metadata;

/// <summary>
/// Represents the result of an AV transcode
/// </summary>
// ReSharper disable once InconsistentNaming
public class AVTranscode
{
    /// <summary>
    /// The S3 uri where the transcode is found 
    /// </summary>
    [JsonProperty("l")]
    public Uri Location { get; set; }
    
    /// <summary>
    /// The name of the transcode present that was used to generate this.
    /// This is the mapped value (e.g. "System preset: Generic 720p") not the policy value ("video-mp4-720p")
    /// </summary>
    [JsonProperty("n")]
    public string TranscodeName { get; set; }
    
    /// <summary>
    /// Extension of transcode (e.g. mp4, mp3, avi)
    /// </summary>
    [JsonProperty("ex")]
    public string Extension { get; set; }
    
    /// <summary>
    /// The mediaType of transcode
    /// </summary>
    /// <remarks>This is currently driven be the extension</remarks>
    [JsonProperty("mt")]
    public string MediaType { get; set; }
    
    /// <summary>
    /// Width of transcode, if Video
    /// </summary>
    [JsonProperty("w")]
    public int? Width { get; set; }
    
    /// <summary>
    /// Height  of transcode, if Video
    /// </summary>
    [JsonProperty("h")]
    public int? Height { get; set; }
    
    /// <summary>
    /// Duration of transcode, in ms
    /// </summary>
    [JsonProperty("d")]
    public long Duration { get; set; }
}
