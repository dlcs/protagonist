using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.FileSystem;

namespace Test.Helpers;

public class FakeFileSystem : IFileSystem
{
    public List<string> CreatedDirectories = new();
    public List<string> DeletedDirectories = new();
    public List<string> DeletedFiles = new();

    public void CreateDirectory(string path) => CreatedDirectories.Add(path);

    public void DeleteDirectory(string path, bool recursive, bool swallowError = true) => DeletedDirectories.Add(path);

    public void DeleteFile(string path, bool swallowError = true) => DeletedFiles.Add(path);

    public bool FileExists(string path) => true;
    public long GetFileSize(string path) => 10;
    public void SetLastWriteTimeUtc(string path, DateTime dateTime)
    {
        // no-op
    }

    public async Task CreateFileFromStream(string path, Stream stream, CancellationToken cancellationToken = default)
    {
        // no-op
    }
}