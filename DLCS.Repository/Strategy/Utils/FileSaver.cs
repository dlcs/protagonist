using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using Microsoft.Extensions.Logging;

namespace DLCS.Repository.Strategy.Utils
{
    public class FileSaver
    {
        private readonly ILogger<FileSaver> logger;

        public FileSaver(ILogger<FileSaver> logger)
        {
            this.logger = logger;
        }
        
        /// <summary>
        /// Save asset from <see cref="OriginResponse"/> to specified file location.
        /// </summary>
        /// <param name="assetId">Id of asset being saved</param>
        /// <param name="originResponse"><see cref="OriginResponse"/> object containing data stream</param>
        /// <param name="destination">Location to store binary to, will be deleted if already exists</param>
        /// <param name="cancellationToken">Async cancellationToken</param>
        /// <returns>ContentLength</returns>
        public async Task<long?> SaveResponseToDisk(AssetId assetId, OriginResponse originResponse, string destination,
            CancellationToken cancellationToken = default)
        {
            if (File.Exists(destination))
            {
                logger.LogInformation("Target file '{File}' already exists, deleting", destination);
                File.Delete(destination);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
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
                    received = originResponse.ContentLength!.Value;
                }
                else
                {
                    // NOTE(DG) This was copied from previous Deliverator implementation, copies and works out size
                    received = await CopyToFileStream(assetStream, fileStream, cancellationToken);
                }
                
                sw.Stop();

                logger.LogDebug(
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