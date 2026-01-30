#nullable disable
using System;
using System.Linq;
using IIIF.Presentation.V3.Strings;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace DLCS.Repository.Converters;

/// <summary>
/// Conversion logic for LanguageMap (on model) -> string (in db)
/// </summary>
public class LanguageMapConverter : ValueConverter<LanguageMap, string>
{
    public LanguageMapConverter()
        : base(
            v => JsonConvert.SerializeObject(v),
            v => JsonConvert.DeserializeObject<LanguageMap>(v))
    {
    }
}

/// <summary>
/// Comparison logic for LanguageMap values. Used by EF internals for determining when a field has changed 
/// </summary>
public class LanguageMapComparer : ValueComparer<LanguageMap>
{
    public LanguageMapComparer()
        : base(
            (c1, c2) => c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c
        )
    {
    }
}
