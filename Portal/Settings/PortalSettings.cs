namespace Portal.Settings
{
    public class PortalSettings
    {
        public string LoginSalt { get; set; }

        /// <summary>
        /// URL for to UniversalViewer
        /// </summary>
        public string UVUrl { get; set; } = "https://universalviewer.io/uv.html";
    }
}