﻿using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace API.Infrastructure.Requests;

[JsonConverter(typeof(StringEnumConverter))]
public enum OperationType
{
    [EnumMember(Value = "unknown")]
    Unknown = 0,
    [EnumMember(Value = "add")]
    Add,
    [EnumMember(Value = "remove")]
    Remove,
    [EnumMember(Value = "replace")]
    Replace,
}
