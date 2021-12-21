namespace DLCS.Model.Auth
{
    /// <summary>
    /// A collection of constants related to auth
    /// </summary>
    public static class Constants
    {
        public static class Profile
        {
            /// <summary>
            /// AuthService profile for IIIF 0 logout services
            /// </summary>
            public static string Logout = "http://iiif.io/api/auth/0/logout";
            
            /// <summary>
            /// AuthService profile for IIIF 0 token services
            /// </summary>
            public static string Token = "http://iiif.io/api/auth/0/token";
        }
    }
}