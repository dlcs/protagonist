using System;
using DLCS.Model.Customers;

namespace DLCS.Repository.Customers;

/// <summary>
/// Exception raised when the regex on a <see cref="CustomerOriginStrategy"/> cannot be evaluated against an origin.
/// </summary>
public class OriginStrategyRegexException(CustomerOriginStrategy strategy, string reason, Exception? inner = null)
    : Exception($"Regex for origin strategy '{strategy.Id}', customer {strategy.Customer}, {reason}", inner)
{
    /// <summary>
    /// Id of the origin strategy that could not be evaluated
    /// </summary>
    public string StrategyId { get; } = strategy.Id;

    /// <summary>
    /// Customer that owns the origin strategy
    /// </summary>
    public int Customer { get; } = strategy.Customer;
}
