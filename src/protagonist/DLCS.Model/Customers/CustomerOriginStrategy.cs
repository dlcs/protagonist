#nullable disable

using System.Diagnostics;

namespace DLCS.Model.Customers;

[DebuggerDisplay("Cust:{Customer}, {Strategy} - {Regex}")]
public partial class CustomerOriginStrategy
{
    public string Id { get; set; }
    public int Customer { get; set; }
    public string Regex { get; set; }
    public OriginStrategyType Strategy { get; set; }
    public string Credentials { get; set; } = string.Empty;
    public bool Optimised { get; set; }
}
