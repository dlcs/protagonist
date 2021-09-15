namespace Orchestrator.Features.Auth
{
    /// <summary>
    /// A collection of helper utils for dealing with auth cookies.
    /// </summary>
    public static class AuthCookieParser
    {
        /// <summary>
        /// Get the Id of auth cookie for customer
        /// </summary>
        public static string GetAuthCookieKey(string cookieNameFormat, int customer)
            => string.Format(cookieNameFormat, customer);

        /// <summary>
        /// Get the cookieValue from CookieId
        /// </summary>
        public static string GetCookieValueForId(string cookieId)
            => $"id={cookieId}";

        /// <summary>
        /// Get the CookieId from cookieValue
        /// </summary>
        public static string GetCookieIdFromValue(string cookieValue)
            => cookieValue[3..];
    }
}