#nullable disable

using System;
using System.Collections.Generic;

namespace DLCS.Repository.Auth;

public partial class SessionUser
{
    public string Id { get; set; }
    public DateTime Created { get; set; }
    
    /// <summary>
    /// A list of Roles this session has access to, split by customer
    /// </summary>
    public Dictionary<int, List<string>> Roles { get; set; }
}
