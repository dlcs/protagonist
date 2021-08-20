using System;
using System.IO;
using System.Threading.Tasks;
using DLCS.Core.Guard;

namespace DLCS.Repository.Strategy
{
    /// <summary>
    /// Represents the result of fetching an asset from an origin.
    /// </summary>
    public class OriginResponse : IAsyncDisposable
    {
        /// <summary>
        /// Get Stream content from origin
        /// </summary>
        public Stream Stream { get; }
        
        /// <summary>
        /// Get value of ContentType for content
        /// </summary>
        public string? ContentType { get; private set; }
        
        /// <summary>
        /// Get value of ContentLength for content
        /// </summary>
        public long? ContentLength { get; private set; }
        
        /// <summary>
        /// Get value indicating if response is empty
        /// </summary>
        public bool IsEmpty { get; private init; }

        /// <summary>
        /// Default EmptyResponse
        /// </summary>
        public static OriginResponse Empty = new(Stream.Null) { IsEmpty = true };

        public OriginResponse(Stream stream)
        {
            Stream = stream.ThrowIfNull(nameof(stream));
        }
        
        public OriginResponse WithContentType(string contentType)
        {
            if (IsEmpty) throw new InvalidOperationException("Cannot set ContentType for empty response");
            ContentType = contentType;
            return this;
        }

        public OriginResponse WithContentLength(long? contentLength)
        {
            if (IsEmpty) throw new InvalidOperationException("Cannot set ContentLength for empty response");
            ContentLength = (contentLength ?? 0) > 0 ? contentLength.Value : (long?) null;
            return this;
        }

        public ValueTask DisposeAsync() 
            => Stream == null ? new ValueTask() : Stream.DisposeAsync();
    }
}