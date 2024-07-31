using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public class SftpReader : ISftpReader
{
    private const int BufferSize = 8192;
    private readonly ISftpWrapper sftpWrapper;
    private ILogger<SftpReader> logger;
    
    public SftpReader(ISftpWrapper sftpWrapper, ILogger<SftpReader> logger)
    {
        this.sftpWrapper = sftpWrapper;
        this.logger = logger;
    }

    public async Task<Stream> RetrieveFile(ConnectionInfo connectionInfo, 
        string path,
        CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetTempFileName();
        Stream outputStream = File.Create(fileName, BufferSize, FileOptions.DeleteOnClose);

        try
        {
            sftpWrapper.DownloadFile(outputStream, path, connectionInfo);

            await outputStream.FlushAsync(cancellationToken);
            outputStream.Position = 0;
        }
        catch (Exception ex)
        {
            if (outputStream.CanRead)
            {
                outputStream.Close();
            }
            
            logger.LogError(ex, "Error downloading SFTP file from Host: {Hostname}, Path: {Path}", connectionInfo.Host, path);
            throw;
        }

        return outputStream;
    }
}