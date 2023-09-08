using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public class SftpReader : ISftpReader
{
    private const int BufferSize = 8192;
    private readonly ISftpWrapper sftpWrapper;
    
    public SftpReader(ISftpWrapper sftpWrapper)
    {
        this.sftpWrapper = sftpWrapper;
    }

    public async Task<Stream> RetrieveFile(ConnectionInfo connectionInfo, 
        string path,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetTempFileName();
        Stream outputStream = File.Create(fileName, BufferSize, FileOptions.DeleteOnClose);
            
        sftpWrapper.DownloadFile(outputStream, path, connectionInfo);
        
        await outputStream.FlushAsync(cancellationToken);
        outputStream.Position = 0;

        return outputStream;
    }
}