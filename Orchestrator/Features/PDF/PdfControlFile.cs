using System;
using Newtonsoft.Json;

namespace Orchestrator.Features.PDF
{
    /// <summary>
    /// The PDF control file is a marker to track the progress of a generated PDF.
    /// </summary>
    public class PdfControlFile
    {
        /// <summary>
        /// The PDF storage key that this control file refers to. 
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }
        
        /// <summary>
        /// A marker for whether the related PDF file exists in storage.
        /// </summary>
        [JsonProperty("exists")]
        public bool Exists { get; set; }
        
        /// <summary>
        /// Whether the related PDF is currently processing.
        /// </summary>
        [JsonProperty("inProcess")]
        public bool InProcess { get; set; }
        
        /// <summary>
        /// Timestamp for when this file was created.
        /// </summary>
        [JsonProperty("created")]
        public DateTime Created { get; set; }
        
        /// <summary>
        /// How many pages are in the target PDF (excluding cover page)
        /// </summary>
        [JsonProperty("pageCount")]
        public int PageCount { get; set; }
        
        /// <summary>
        /// The size of the related PDF.
        /// </summary>
        [JsonProperty("sizeBytes")]
        public int SizeBytes { get; set; }

        /// <summary>
        /// Check if this is control file is stale (in process for longer than X secs)
        /// </summary>
        public bool IsStale(int staleSecs)
            => InProcess && DateTime.Now.Subtract(Created).TotalSeconds > staleSecs;
    }
}