using System.Collections.Generic;
using IIIF.Presentation.V3.Strings;

namespace API.Converters;

public static class LanguageMapX
{
    public static LanguageMap? ToLanguageMap(this Dictionary<string, List<string>>? dictionaryToConvert)
    {
        if (dictionaryToConvert == null) return null;
        
        var lm = new LanguageMap();
        
        foreach (var keyValueToConvert in dictionaryToConvert)
        {
            lm.Add(keyValueToConvert.Key, keyValueToConvert.Value);
        }

        return lm;
    }
}
