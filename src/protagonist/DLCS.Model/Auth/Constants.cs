namespace DLCS.Model.Auth;

/// <summary>
/// A collection of constants related to auth
/// </summary>
public static class Constants
{
    public static class ProfileV0
    {
        /// <summary>
        /// AuthService profile for IIIF 0 logout services
        /// </summary>
        /// <remarks>For backwards compatibility</remarks>
        public static string Logout = "http://iiif.io/api/auth/0/logout";
        
        /// <summary>
        /// AuthService profile for IIIF 0 token services
        /// </summary>
        /// <remarks>For backwards compatibility</remarks>
        public static string Token = "http://iiif.io/api/auth/0/token";
    }
    
    public static class ProfileV1
    {
        /// <summary>
        /// AuthService profile for IIIF 1 logout services
        /// </summary>
        public static string Logout = "http://iiif.io/api/auth/1/logout";
        
        /// <summary>
        /// AuthService profile for IIIF 1 token services
        /// </summary>
        public static string Token = "http://iiif.io/api/auth/1/token";
    }
}