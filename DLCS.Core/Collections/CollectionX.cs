using System.Collections.Generic;
using System.Linq;

namespace DLCS.Core.Collections
{
    public static class CollectionX
    {
        /// <summary>
        /// Check if IEnumerable is null or empty
        /// </summary>
        /// <returns>true if null or empty, else false</returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection) => collection == null || !collection.Any();
        
        /// <summary>
        /// Check if IList is null or empty
        /// </summary>
        /// <returns>true if null or empty, else false</returns>
        public static bool IsNullOrEmpty<T>(this IList<T>? collection) => collection == null || collection.Count == 0;
    }
}