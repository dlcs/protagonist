using System;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Guard;
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
        customerOriginStrategy.ThrowIfNull(nameof(customerOriginStrategy));
        
        var basicCredentials =
            await credentialsRepository.GetBasicCredentialsForOriginStrategy(customerOriginStrategy!);

        basicCredentials.ThrowIfNull(nameof(basicCredentials));

        var originUri = new Uri(origin);

        // The URI class doesn't know what the default port is for SFTP, so defaults to -1
        var port = originUri.IsDefaultPort ? DefaultPort : originUri.Port;

        ConnectionInfo connectionInfo = new ConnectionInfo(originUri.Host, port, basicCredentials!.User,
            ProxyTypes.None, originUri.Host, port, basicCredentials.User, 
            basicCredentials.Password, new PasswordAuthenticationMethod(basicCredentials.User, 
                basicCredentials.Password));

        try
        {
            var outputStream = await sftpReader.RetrieveFile(connectionInfo, originUri.AbsolutePath, cancellationToken);
           return new OriginResponse(outputStream);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error retrieving file from SFTP server - {Origin}", origin);
            throw;
        }
    }
}