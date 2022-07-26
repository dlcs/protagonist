using IIIF;

namespace DLCS.Model.Assets
{
    /// <summary>
    /// Size candidate for a single longest edge.
    /// </summary>
    public class SizeCandidate
    {
        public int? LongestEdge { get; }
        
        public bool KnownSize { get; }
        
        public SizeCandidate(int? longestEdge)
        {
            LongestEdge = longestEdge;
            KnownSize = longestEdge.HasValue;
        }

        public SizeCandidate()
        {
        }
    }

    /// <summary>
    /// Size candidates for size where no exact match is found.
    /// </summary>
    public class ResizableSize : SizeCandidate
    {
        public Size? LargerSize { get; init;}
        public Size? SmallerSize { get; init;}
        public Size Ideal { get; init; }

        public ResizableSize(int? longestEdge) : base(longestEdge)
        {
        }

        public ResizableSize()
        {
        }
    }
}