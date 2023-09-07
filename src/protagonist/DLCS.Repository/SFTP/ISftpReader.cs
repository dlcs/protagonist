using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Repository.Strategy;
using Renci.SshNet;


namespace DLCS.Repository.SFTP;

public interface ISftpReader
{
    Task<Stream> RetrieveFile(ConnectionInfo connectionInfo, string path,
        CancellationToken cancellationToken = default);
}