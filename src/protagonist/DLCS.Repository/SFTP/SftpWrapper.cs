using System.IO;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public class SftpWrapper : ISftpWrapper
{
    public void DownloadFile(Stream stream, string fileLocation, ConnectionInfo connectionInfo)
    {
        using var client = new SftpClient(connectionInfo);
        client.Connect();
        client.DownloadFile(fileLocation, stream);
    }
}