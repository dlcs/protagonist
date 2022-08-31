using System;
using System.Diagnostics;

namespace DLCS.Model.Assets;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public class Batch
{
    public int Id { get; set; }
    public int Customer { get; set; }
    public DateTime Submitted { get; set; }
    public int Count { get; set; }
    public int Completed { get; set; }
    public int Errors { get; set; }
    public DateTime? Finished { get; set; }
    public bool Superseded { get; set; }
    
    private string DebuggerDisplay => $"{Id}, Cust:{Customer}, {Count} item(s)";
}
