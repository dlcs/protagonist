using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DLCS.HydraModel
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AssetFamily
    {        
        /// <summary>
        /// Represents an image asset.
        /// </summary>
        [EnumMember(Value = "I")]
        Image = 'I',
        
        /// <summary>
        /// Represents a time based asset (audio or video).
        /// </summary>
        [EnumMember(Value = "T")]
        Timebased = 'T',
        
        /// <summary>
        /// Represents a file asset (pdf, docx etc).
        /// </summary>
        [EnumMember(Value = "F")]
        File = 'F'
    }
}