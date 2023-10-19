using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace DLCS.Core.Streams;

public static class StreamX
{
    /// <summary>
    /// Check if Stream is null or Stream.Null 
    /// </summary>
    /// <param name="stream">Stream to check</param>
    /// <returns>True if stream is null</returns>
    public static bool IsNull([NotNullWhen(false)]this Stream? stream)
        => stream == null || stream == Stream.Null;
}