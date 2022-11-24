using System.Diagnostics;

namespace DLCS.Model.Policies;

/// <summary>
/// Represents set of instructions on how to process and image.
/// The format of the 'TechnicalDetails' will depend on the downstream system carrying out the changes.
/// e.g. they may be ElasticTranscoder presets, or a set of instructions for kdu
/// </summary>
[DebuggerDisplay("{Name}")]
public class ImageOptimisationPolicy
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string[] TechnicalDetails { get; set; }
    
    /// <summary>
    /// Customer that this optimisation policy is for
    /// </summary>
    public int Customer { get; set; }
    
    /// <summary>
    /// If true, optimisation policy is for all customers
    /// </summary>
    public bool Global { get; set; }
}

public static class ImageOptimisationPolicyX
{
    // Id of imageOptimisationPolicy for no-op
    private const string NoneId = "none";

    /// <summary>
    /// Check if this policy is the special no-op/no-transcode policy
    /// </summary>
    public static bool IsNotProcessed(this ImageOptimisationPolicy policy) => policy.Id == NoneId;
    
    /// <summary>
    /// Check if specified policy Id is the special no-op/no-transcode policy
    /// </summary>
    /// <param name="policyId"></param>
    /// <returns></returns>
    public static bool IsNotProcessedIdentifier(string? policyId) => policyId == NoneId;
}