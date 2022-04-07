﻿using System;
using System.Text;

namespace DLCS.Core.Strings
{
    public static class StringX
    {
        /// <summary>
        /// Check if string has content (is not null, empty or whitespace)
        /// </summary>
        /// <param name="str">String to check</param>
        /// <returns>true if string contains content; else false</returns>
        public static bool HasText(this string str) => !string.IsNullOrWhiteSpace(str);

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
        /// <param name="str"></param>
        /// <returns>The camel case string</returns>
        public static string ToCamelCase(this string str)
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
            return sb.ToString();
        }
    }
}
