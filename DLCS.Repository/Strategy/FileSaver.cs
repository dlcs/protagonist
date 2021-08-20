using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy
{
    public class FileSaver
    {
        private readonly ILogger<FileSaver> logger;

        public FileSaver(ILogger<FileSaver> logger)
        {
            this.logger = logger;
        }
        
        public async Task<long?> SaveResponseToDisk(AssetId assetId, OriginResponse originResponse, string destination,
            CancellationToken cancellationToken = default)
        {
            if (File.Exists(destination))
            {
                logger.LogInformation("Target file '{File}' already exists, deleting", destination);
                File.Delete(destination);
            }

            try
            {
                var sw = Stopwatch.StartNew();
                await using var fileStream = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.Write);
                var assetStream = originResponse.Stream;

                bool knownFileSize = originResponse.ContentLength.HasValue;
                long received;

                if (knownFileSize)
                {
                    await assetStream.CopyToAsync(fileStream, cancellationToken);
                    received = originResponse.ContentLength.Value;
                }
                else
                {
                    // NOTE(DG) This was copied from previous Deliverator implementation, copies and works out size
                    received = await CopyToFileStream(assetStream, fileStream, cancellationToken);
                }
                
                sw.Stop();

                logger.LogInformation(
                    "Download {Asset} to '{TargetPath}': done ({Bytes} bytes, {Elapsed}ms) using {CopyType}",
                    assetId, destination, received, sw.ElapsedMilliseconds,
                    knownFileSize ? "framework-copy" : "manual-copy");

                return received;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error writing file to disk. destination: {Destination}", destination);
                throw;
            }
        }

        private static async Task<long> CopyToFileStream(Stream assetStream, FileStream fileStream,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[102400];
            int size;
            long received = 0;

            while ((size = await assetStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, size, cancellationToken);
                received += size;
                await fileStream.FlushAsync(cancellationToken);
            }

            fileStream.Close();
            assetStream.Close();
            return received;
        }
    }
}