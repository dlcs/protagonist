
namespace Orchestrator.Settings
{
    public class OrchestratorSettings
    {
        public string PathBase { get; set; }

        public ProxySettings Proxy { get; set; }
    }

    public class ProxySettings
    {
        /// <summary>
        /// Get the root path that thumb handler is listening on
        /// </summary>
        public string ThumbsPath { get; set; } = "thumbs";

        /// <summary>
        /// Get value of whether to check for UV thumbs (90,)
        /// </summary>
        public bool CheckUVThumbs { get; set; } = true;

        /// <summary>
        /// Get Size parameter for any UV thumb redirect.
        /// </summary>
        public string UVThumbReplacementPath { get; set; } = "!200,200";
        
        /// <summary>
        /// Get https base url for region.
        /// </summary>
        public string S3HttpBase { get; set; }
        
        /// <summary>
        /// Get the S3 storage bucket name.
        /// </summary>
        public string StorageBucket { get; set; }
    }
}