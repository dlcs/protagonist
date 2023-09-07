using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace DLCS.Repository.SFTP;

public class SftpReader : ISftpReader
{
    public async Task<Stream> RetrieveFile(ConnectionInfo connectionInfo, string path,
        CancellationToken cancellationToken = default)
    {
        Stream outputStream;
        
        using (var client = new SftpClient(connectionInfo))
        {
            client.Connect();
            outputStream = new MemoryStream();
            client.DownloadFile(path, outputStream);
            await outputStream.FlushAsync(cancellationToken);
            outputStream.Position = 0;
        }

        return outputStream;
    }
    
    /// <summary>
    /// Asynchronously download the file into the stream.
    /// </summary>
    /// <param name="client">The <see cref="SftpClient"/> instance</param>
    /// <param name="path">Remote file path.</param>
    /// <param name="factory">The <see cref="System.Threading.Tasks.TaskFactory">TaskFactory</see> used to create the Task</param>
    /// <param name="creationOptions">The TaskCreationOptions value that controls the behavior of the
    /// created <see cref="T:System.Threading.Tasks.Task">Task</see>.</param>
    /// <param name="scheduler">The <see cref="System.Threading.Tasks.TaskScheduler">TaskScheduler</see>
    /// that is used to schedule the task that executes the end method.</param>
    /// <returns></returns>
    private Task<Stream> DownloadAsync(SftpClient client,
        string path,
        TaskFactory? factory = null,
        TaskCreationOptions creationOptions = default,
        TaskScheduler? scheduler = null)
    {
        var output = new MemoryStream();
        
        (factory ??= Task.Factory).FromAsync(
            client.BeginDownloadFile(path, output),
            client.EndDownloadFile,
            creationOptions, scheduler ?? factory.Scheduler ?? TaskScheduler.Current);

        var task = new Task<Stream>(() => output);
        return task;
    }
}