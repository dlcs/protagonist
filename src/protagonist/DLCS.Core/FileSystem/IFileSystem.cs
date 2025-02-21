using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Core.FileSystem;

/// <summary>
/// Interface for interactions with underlying file system.
/// Used to avoid issues where tests need to change filesystem
/// </summary>
public interface IFileSystem
{
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive, bool swallowError = true);
    void DeleteFile(string path, bool swallowError = true);
    bool FileExists(string path);
    long GetFileSize(string path);
    void SetLastWriteTimeUtc(string path, DateTime dateTime);
    Task CreateFileFromStream(string path, Stream stream, CancellationToken cancellationToken = default);
}