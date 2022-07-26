using System;
using System.IO;
using System.Threading.Tasks;

namespace Thumbs;

/// <summary>
/// Represents request to get a Thumbnail.
/// </summary>
/// <see cref="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-disposeasync"/>
public class ThumbnailResponse : IDisposable, IAsyncDisposable
{
    public static readonly ThumbnailResponse Empty = new ThumbnailResponse();
    
    /// <summary>
    /// True if thumbnail was an exact match for request.
    /// </summary>
    public bool IsExactMatch { get; private set; }
    
    /// <summary>
    /// True if result was resized from generated thumbnail.
    /// </summary>
    public bool WasResized { get; private set; }
    
    /// <summary>
    /// Thumbnail data, either streamed from S3 or resized on the fly.
    /// </summary>
    public Stream? ThumbnailStream { get; private set; }

    /// <summary>
    /// True if no thumbnail has been found.
    /// </summary>
    public bool IsEmpty => ThumbnailStream == null;
    
    public static ThumbnailResponse ExactSize(Stream? stream) 
        => new() {IsExactMatch = true, ThumbnailStream = stream};
    
    public static ThumbnailResponse Resized(Stream? stream) 
        => new() {WasResized = true, ThumbnailStream = stream};

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();

        Dispose(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    private async ValueTask DisposeAsyncCore()
    {
        // Cascade async dispose calls
        if (ThumbnailStream != null)
        {
            await ThumbnailStream.DisposeAsync();
            ThumbnailStream = null;
        }
    }

    private bool disposed = false;
    protected virtual void Dispose(bool disposing)
    {
        if (disposed) return;

        if (disposing) ThumbnailStream?.Dispose();

        disposed = true;
    }
}