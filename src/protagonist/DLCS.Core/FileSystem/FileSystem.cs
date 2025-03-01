using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DLCS.Core.FileSystem;

public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void DeleteDirectory(string path, bool recursive, bool swallowError = true)
    {
        try
        {
            Directory.Delete(path, recursive);
        }
        catch (Exception)
        {
            if (!swallowError) throw;
        }
    }

    public void DeleteFile(string path, bool swallowError = true)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            if (!swallowError) throw;
        }
    }

    public bool FileExists(string path) => File.Exists(path);
    public long GetFileSize(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            return fi.Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public void SetLastWriteTimeUtc(string path, DateTime dateTime) => File.SetLastWriteTimeUtc(path, dateTime);
    
    public async Task CreateFileFromStream(string path, Stream stream, CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(path, FileMode.Create);
        await stream.CopyToAsync(fileStream, cancellationToken);
    }
}