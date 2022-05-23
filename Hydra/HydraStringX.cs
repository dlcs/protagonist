using System;
using System.Linq;

namespace Hydra
{
    public static class HydraStringX
    {
        public static string? GetLastPathElement(this string? path, string? requiredPrecedingPath = null)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var parts = path.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
            var result = parts.Count < 2 ? null : parts[^1];
            if (requiredPrecedingPath == null)
            {
                return result;
            }

            if (path.EndsWith(requiredPrecedingPath + result))
            {
                return result;
            }

            return null;
        }

        public static int? GetLastPathElementAsInt(this string? path)
        {
            var last = path.GetLastPathElement();
            if (string.IsNullOrWhiteSpace(last))
            {
                return null;
            }

            // We want this to throw if not an int
            return int.Parse(last);
        }
    }
}