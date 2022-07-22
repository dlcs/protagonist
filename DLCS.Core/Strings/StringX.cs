using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DLCS.Core.Strings
{
    public static class StringX
    {
        /// <summary>
        /// Check if string has content (is not null, empty or whitespace)
        /// </summary>
        /// <param name="str">String to check</param>
        /// <returns>true if string contains content; else false</returns>
        public static bool HasText([NotNullWhen(true)] this string? str) => !string.IsNullOrWhiteSpace(str);

        /// <summary>
        /// Decode base64 encoded string back to UTF8 representation
        /// </summary>
        /// <param name="encoded">Base64 encoded string</param>
        /// <returns>Decoded string</returns>
        public static string DecodeBase64(this string encoded)
            => encoded.HasText()
                ? Encoding.UTF8.GetString(Convert.FromBase64String(encoded))
                : encoded;

        /// <summary>
        /// converts "Some list of strings" to "someListOfStrings"
        /// </summary>
        /// <param name="str">The string to transform</param>
        /// <param name="lowerInitial">Force the first letter to be lower case</param>
        /// <returns>The camel case string</returns>
        public static string ToCamelCase(this string str, bool lowerInitial = false)
        {
            var sb = new StringBuilder();
            bool previousWasSpace = false;
            foreach (char c in str.Trim())
            {
                if (Char.IsLetterOrDigit(c))
                {
                    sb.Append(previousWasSpace ? Char.ToUpperInvariant(c) : c);
                }
                previousWasSpace = Char.IsWhiteSpace(c);
            }

            if (lowerInitial)
            {
                sb[0] = Char.ToLower(sb[0]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Build string concatenated with specified separator. Will ensure only 1 separator between elements 
        /// </summary>
        /// <param name="str">Initial string to add further strings to</param>
        /// <param name="separator">Separator to place between initial string + further strings</param>
        /// <param name="toAppend">List of strings to add, separated by separator</param>
        /// <returns></returns>
        public static string ToConcatenated(this string str, char separator, params string[] toAppend)
        {
            if (string.IsNullOrWhiteSpace(str)) return str;

            var sb = new StringBuilder(str.TrimEnd(separator));
            foreach (var s in toAppend)
            {
                sb.Append(separator);
                sb.Append(s.TrimEnd(separator).TrimStart(separator));
            }

            return sb.ToString();
        }
        
        
        public static bool IsValidEmail(this string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                    RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Examines the domain part of the email and normalizes it.
                string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    string domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException e)
            {
                return false;
            }
            catch (ArgumentException e)
            {
                return false;
            }

            try
            {
                return Regex.IsMatch(email,
                    @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                    RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
    }
}