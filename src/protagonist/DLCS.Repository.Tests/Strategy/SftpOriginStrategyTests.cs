using System;
using System.IO;
using System.Threading;
using DLCS.Core.Types;
using DLCS.Model.Auth;
using DLCS.Model.Customers;
using DLCS.Repository.SFTP;
using DLCS.Repository.Strategy;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DLCS.Repository.Tests.Strategy;

public class SftpOriginStrategyTests
{
    private readonly SftpOriginStrategy sut;
    private readonly ISftpReader sftpReader;
    private readonly ICredentialsRepository credentialsRepository;
    private readonly AssetId assetId = new(15, 25, "foo");

    public SftpOriginStrategyTests()
    {
        sftpReader = A.Fake<ISftpReader>();
        credentialsRepository = A.Fake<ICredentialsRepository>();

        sut = new SftpOriginStrategy(credentialsRepository, sftpReader, new NullLogger<SftpOriginStrategy>());
    }

    [Fact]
    public async Task LoadAssetFromOrigin_ReturnsExpectedResponse_OnSuccess()
    {
        // Arrange
        var content = "this is a test";
        
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        await writer.FlushAsync();
        stream.Position = 0;
        
        const string originUri = "sftp://www.someuri.com/public_ftp/someId";

        var basicCredentials = new BasicCredentials()
        {
            User = "correctTest",
            Password = "correctPassword"
        };
        
        var customerOriginStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.SFTP,
            Id = "correctResponse"
        };

        A.CallTo(() =>
            credentialsRepository.GetBasicCredentialsForOriginStrategy(
                A<CustomerOriginStrategy>.That.Matches(a => a.Id == "correctResponse")))
            .Returns(basicCredentials);
        
        A.CallTo(() =>
                sftpReader.RetrieveFile(
                    A<ConnectionInfo>.That.Matches(a => a.Username == basicCredentials.User), 
                    A<string>._,
                    A<CancellationToken>._))
            .Returns(stream);

        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        A.CallTo(() =>
                credentialsRepository.GetBasicCredentialsForOriginStrategy(
                    A<CustomerOriginStrategy>.That.Matches(a => a.Id == "correctResponse")))
            .MustHaveHappened();
        A.CallTo(() =>
                sftpReader.RetrieveFile(
                    A<ConnectionInfo>.That.Matches(a => a.Username == basicCredentials.User), 
                    A<string>._,
                    A<CancellationToken>._))
            .MustHaveHappened();
        result.Stream.Should().NotBeNull().And.Subject.Should().NotBeSameAs(Stream.Null);
        result.ContentLength.Should().Be(stream.Length);
    }
    
    [Fact]
    public async Task LoadAssetFromOrigin_ReturnsNull_IfCallFailsToFindCredentials()
    {
        // Arrange
        const string originUri = "sftp://www.someuri.com/public_ftp/someId";
        
        var customerOriginStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.SFTP,
            Id = "errorResponse"
        };

        BasicCredentials credentials = null;

        A.CallTo(() =>
                credentialsRepository.GetBasicCredentialsForOriginStrategy(
                    A<CustomerOriginStrategy>.That.Matches(a => a.Id == "errorResponse")))
            .Returns(credentials);
        
        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        result.Stream.Should().BeSameAs(Stream.Null);
        result.IsEmpty.Should().BeTrue();
    }
    
    [Fact]
    public async Task LoadAssetFromOrigin_ReturnsNull_IfCallFails()
    {
        // Arrange
        const string originUri = "sftp://www.someuri.com/public_ftp/someId";
        
        var basicCredentials = new BasicCredentials()
        {
            User = "correctTest",
            Password = "correctPassword"
        };

        var customerOriginStrategy = new CustomerOriginStrategy
        {
            Strategy = OriginStrategyType.SFTP,
            Id = "notFound"
        };

        A.CallTo(() =>
                credentialsRepository.GetBasicCredentialsForOriginStrategy(
                    A<CustomerOriginStrategy>.That.Matches(a => a.Id == "notFound")))
            .Returns(basicCredentials);
        
        A.CallTo(() =>
                sftpReader.RetrieveFile(
                    A<ConnectionInfo>.That.Matches(a => a.Username == basicCredentials.User), 
                    A<string>._,
                    A<CancellationToken>._))
            .Throws<SftpPathNotFoundException>();
        
        // Act
        var result = await sut.LoadAssetFromOrigin(assetId, originUri, customerOriginStrategy);
        
        // Assert
        result.Stream.Should().BeSameAs(Stream.Null);
        result.IsEmpty.Should().BeTrue();
    }
}