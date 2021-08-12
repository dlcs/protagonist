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
        public Stream Stream { get; }
        public string ContentType { get; private set; }
        public long? ContentLength { get; private set; }

        public OriginResponse(Stream stream)
        {
            Stream = stream.ThrowIfNull(nameof(stream));
        }
        
        public OriginResponse WithContentType(string contentType)
        {
            ContentType = contentType;
            return this;
        }

        public OriginResponse WithContentLength(long? contentLength)
        {
            ContentLength = (contentLength ?? 0) > 0 ? contentLength.Value : (long?) null;
            return this;
        }

        public ValueTask DisposeAsync() 
            => Stream == null ? new ValueTask() : Stream.DisposeAsync();
    }
}