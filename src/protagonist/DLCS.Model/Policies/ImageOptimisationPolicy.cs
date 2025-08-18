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
    /// <summary>
    /// Unique identifier for policy, e.g. "fast-higher", "video-max" etc
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// Friendly name for policy
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// A collection of strings, contents are relevant to target technology
    /// </summary>
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

public static class KnownImageOptimisationPolicy
{
    // Id of imageOptimisationPolicy for no-op
    public const string NoneId = "none";
    
    // Id of imageOptimisationPolicy for use-original. Signifies source image is tile-ready
    public const string UseOriginalId = "use-original";

    /// <summary>
    /// Check if specified policy Id is the special no-op/no-transcode policy
    /// </summary>
    public static bool IsNoOpIdentifier(string? policyId) => policyId == NoneId;
    
    /// <summary>
    /// Check if this policy is the special 'use-original' policy
    /// </summary>
    public static bool IsUseOriginal(this ImageOptimisationPolicy policy) => policy.Id == UseOriginalId;
    
    /// <summary>
    /// Check if specified policy Id is the special 'use-original' policy
    /// </summary>
    public static bool IsUseOriginalIdentifier(string? policyId) => policyId == UseOriginalId;
}