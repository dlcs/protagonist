using System.Collections.Generic;
using DLCS.Core.Types;

namespace DLCS.Model;

/// <summary>
/// An object that contains the minimal properties to identify an adjunct
/// </summary>
public class AdjunctIdentifierOnly
{
    public string Id { get; set; }

    public List<string> Adjunct { get; set; }
}
