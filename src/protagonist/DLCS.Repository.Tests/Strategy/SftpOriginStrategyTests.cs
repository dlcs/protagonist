using DLCS.Model.Auth;
using DLCS.Repository.SFTP;
using DLCS.Repository.Strategy;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

namespace DLCS.Repository.Tests.Strategy;

public class SftpOriginStrategyTests
{
    private readonly SftpOriginStrategy sut;
    private readonly ISftpReader sftpReader;
    private readonly ICredentialsRepository credentialsRepository;
    
    public SftpOriginStrategyTests()
    {
        sftpReader = A.Fake<ISftpReader>();
        credentialsRepository = A.Fake<ICredentialsRepository>();

        sut = new SftpOriginStrategy(credentialsRepository, sftpReader, new NullLogger<SftpOriginStrategy>());
    }
}