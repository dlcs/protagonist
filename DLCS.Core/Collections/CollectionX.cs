using System.Collections.Generic;
using System.Linq;

namespace DLCS.Core.Collections
{
    public static class CollectionX
    {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection) => collection == null || !collection.Any();
    }
}