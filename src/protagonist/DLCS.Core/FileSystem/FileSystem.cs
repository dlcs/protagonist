using System;

namespace DLCS.Core.FileSystem;

public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) => System.IO.Directory.CreateDirectory(path);
    public void DeleteDirectory(string path, bool recursive, bool swallowError = true)
    {
        try
        {
            System.IO.Directory.Delete(path, recursive);
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
            System.IO.File.Delete(path);
        }
        catch (Exception)
        {
            if (!swallowError) throw;
        }
    }

    public bool FileExists(string path) => System.IO.File.Exists(path);
}