using System.Linq;

namespace DLCS.Core.Collections;

public static class StringArrays
{
    /// <summary>
    /// Ensure that newString is contained within returned array.
    /// If string already exists it is not added again.
    /// </summary>
    public static string[] EnsureString(string[]? strings, string newString)
    {
        if (strings == null || strings.Length == 0)
        {
            return new[] {newString};
        }

        if (strings.Contains(newString))
        {
            return strings;
        }
        
        return strings.Append(newString).ToArray();
    }
    
    
    public static string[] RemoveString(string[]? strings, string toRemove)
    {
        if (strings == null || strings.Length == 0)
        {
            return System.Array.Empty<string>();
        }
        if (!strings.Contains(toRemove))
        {
            return strings;
        }
        var list = strings.ToList();
        list.RemoveAll(s => s == toRemove);
        return list.ToArray();
    }
}