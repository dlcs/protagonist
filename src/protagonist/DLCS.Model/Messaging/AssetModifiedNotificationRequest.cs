﻿using System.Collections.Generic;
using DLCS.Model.Assets;
using DLCS.Model.PathElements;
using Newtonsoft.Json;

namespace DLCS.Model.Messaging;

public class AssetDeletedNotificationRequest
{
    public Asset? Asset { get; set; }

    public CustomerPathElement? CustomerPathElement { get; set; }
    
    [JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public ImageCacheType DeleteFrom { get; set; }
}