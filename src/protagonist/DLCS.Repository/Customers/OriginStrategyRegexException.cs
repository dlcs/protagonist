using System;
using DLCS.Model.Customers;

namespace DLCS.Repository.Customers;

/// <summary>
/// Exception raised when the regex on a <see cref="CustomerOriginStrategy"/> cannot be evaluated against an origin.
/// </summary>
/// <remarks>
/// This is deliberately fatal - strategies are matched in <see cref="CustomerOriginStrategy.Order"/> and silently
/// skipping one that can't be evaluated could result in a lower priority strategy being used to fetch the origin
/// with the wrong credentials.
/// </remarks>
public class OriginStrategyRegexException : Exception
{
    /// <summary>
    /// Id of the origin strategy that could not be evaluated
    /// </summary>
    public string StrategyId { get; }

    /// <summary>
    /// Customer that owns the origin strategy
    /// </summary>
    public int Customer { get; }

    public OriginStrategyRegexException(CustomerOriginStrategy strategy, string reason, Exception? inner = null)
        : base($"Regex for origin strategy '{strategy.Id}', customer {strategy.Customer}, {reason}", inner)
    {
        StrategyId = strategy.Id;
        Customer = strategy.Customer;
    }
}
