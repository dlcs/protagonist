using System.Linq;
using DLCS.Core.Strings;

namespace Orchestrator.Infrastructure.DataAccess;

/// <summary>
/// Helpers for mapping raw Dapper query results onto model properties.
/// </summary>
public static class DapperColumnMapping
{
    /// <summary>
    /// Split a comma-delimited string column (e.g. Roles, Tags) into an array.
    /// </summary>
    /// <remarks>EF handles this via a value-converter but Dapper doesn't</remarks>
    public static string[] SplitDelimited(string? value)
        => value.SplitSeparatedString(",").ToArray();
}
