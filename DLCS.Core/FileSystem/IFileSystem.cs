namespace DLCS.Core.FileSystem;

/// <summary>
/// Interface for interactions with underlying file system.
/// Used to avoid issues where tests need to change filesystem
/// </summary>
public interface IFileSystem
{
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool rescursive);
    void DeleteFile(string path);
    bool FileExists(string path);
}

public class FileSystem : IFileSystem
{
    public void CreateDirectory(string path) => System.IO.Directory.CreateDirectory(path);
    public void DeleteDirectory(string path, bool rescursive) => System.IO.Directory.Delete(path, rescursive);

    public void DeleteFile(string path) => System.IO.File.Delete(path);
    public bool FileExists(string path) => System.IO.File.Exists(path);
}