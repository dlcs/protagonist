using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Orchestrator.Infrastructure.NamedQueries.Persistence.Models
{
    /// <summary>
    /// The PDF control file is a marker to track the progress of a generated PDF.
    /// </summary>
    public class ControlFile
    {
        /// <summary>
        /// The storage key that this control file refers to. 
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }
        
        /// <summary>
        /// A marker for whether the related file exists in storage.
        /// </summary>
        [JsonProperty("exists")]
        public bool Exists { get; set; }
        
        /// <summary>
        /// Whether the related file is currently processing.
        /// </summary>
        [JsonProperty("inProcess")]
        public bool InProcess { get; set; }
        
        /// <summary>
        /// Timestamp for when this file was created.
        /// </summary>
        [JsonProperty("created")]
        public DateTime Created { get; set; }
        
        /// <summary>
        /// How many assets are in the target resource
        /// </summary>
        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }
        
        /// <summary>
        /// The size of the related resource
        /// </summary>
        [JsonProperty("sizeBytes")]
        public long SizeBytes { get; set; }
        
        /// <summary>
        /// List of unique roles for all images included in related projection
        /// </summary>
        [JsonProperty("roles")]
        public List<string>? Roles { get; set; }

        /// <summary>
        /// Check if this is control file is stale (in process for longer than X secs)
        /// </summary>
        public bool IsStale(int staleSecs)
            => InProcess && DateTime.UtcNow.Subtract(Created).TotalSeconds > staleSecs;
    }
}