using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using DLCS.Core.Enum;

namespace DLCS.Model.Assets;

[Flags]
[JsonConverter(typeof(FlagConverter<ImageCacheType>))]
public enum ImageCacheType
{
    None = 1,
    Unknown = 2,
    [Description("cdn")]
    Cdn = 4,
    [Description("internalCache")]
    InternalCache = 8
}