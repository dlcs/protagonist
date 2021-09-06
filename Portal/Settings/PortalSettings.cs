namespace Portal.Settings
{
    public class PortalSettings
    {
        public string LoginSalt { get; set; }
        
        /// <summary>
        /// String used for salting requests to API
        /// </summary>
        public string ApiSalt { get; set; }

        /// <summary>
        /// URL for viewing manifest in UniversalViewer
        /// </summary>
        public string UVUrl { get; set; } = "https://universalviewer.io/uv.html";
        
        /// <summary>
        /// URL for viewing manifest in Mirador
        /// </summary>
        public string MiradorUrl { get; set; } = "https://projectmirador.org/embed/";
    }
}