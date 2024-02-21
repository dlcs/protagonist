using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public interface ISftpReader
{
    /// <summary>
    /// Retrieves a file from an SFTP server
    /// </summary>
    /// <param name="connectionInfo">details of the SFTP server</param>
    /// <param name="path">The path to the file in the SFTP server</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A stream of data containing the file</returns>
    Task<Stream> RetrieveFile(ConnectionInfo connectionInfo, 
        string path,
        CancellationToken cancellationToken = default);
}