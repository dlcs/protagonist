using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository.SFTP;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace DLCS.Repository.Strategy;

/// <summary>
/// OriginStrategy implementation for 'sftp' assets.
/// </summary>
public class SftpOriginStrategy : IOriginStrategy
{
    private readonly ICredentialsRepository credentialsRepository;
    private readonly ILogger<SftpOriginStrategy> logger;
    private readonly ISftpReader sftpReader;
    private const int DefaultPort = 22;

    public SftpOriginStrategy(ICredentialsRepository credentialsRepository, 
        ISftpReader sftpReader,
        ILogger<SftpOriginStrategy> logger)
    {
        this.credentialsRepository = credentialsRepository;
        this.logger = logger;
        this.sftpReader = sftpReader;
    }
    
    public async Task<OriginResponse> LoadAssetFromOrigin(AssetId assetId, string origin,
        CustomerOriginStrategy? customerOriginStrategy, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Fetching {Asset} from Origin: {Origin}", assetId, origin);
        
        var basicCredentials =
            await credentialsRepository.GetBasicCredentialsForOriginStrategy(customerOriginStrategy!);

        if (basicCredentials == null)
        {
            throw new ApplicationException(
                $"Could not find credentials for customerOriginStrategy {customerOriginStrategy?.Id}");
        }
        
        var originUri = new Uri(origin);

        // The URI class doesn't know what the default port is for SFTP, so defaults to -1
        var port = originUri.IsDefaultPort ? DefaultPort : originUri.Port;

        ConnectionInfo connectionInfo = GetConnectionInfo(originUri, port, basicCredentials);

        try
        {
            var originPath = Uri.UnescapeDataString(originUri.AbsolutePath);
            var outputStream = await sftpReader.RetrieveFile(connectionInfo, originPath, cancellationToken);
            return new OriginResponse(outputStream).WithContentLength(outputStream.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching {Asset} from Origin: {Origin}", assetId, origin);
            return OriginResponse.Empty;
        }
    }

    private static ConnectionInfo GetConnectionInfo(Uri originUri, int port, BasicCredentials basicCredentials)
    {
        return new ConnectionInfo(originUri.Host, port, basicCredentials!.User,
            ProxyTypes.None, originUri.Host, port, basicCredentials.User, 
            basicCredentials.Password, new PasswordAuthenticationMethod(basicCredentials.User, 
                basicCredentials.Password));
    }
}