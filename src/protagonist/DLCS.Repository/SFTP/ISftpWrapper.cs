using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public interface ISftpWrapper
{
    /// <summary>
    /// Downloads a file from SFTP
    /// </summary>
    /// <param name="stream">The stream to use</param>
    /// <param name="fileLocation">The location of the file in the SFTP server</param>
    /// <param name="connectionInfo">Details to connect to the SFTP server</param>
    void DownloadFile(Stream stream, string fileLocation, ConnectionInfo connectionInfo);
}