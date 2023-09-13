using System;
using System.IO;
using DLCS.Repository.SFTP;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DLCS.Repository.Tests.SFTP;

public class SftpReaderTests
{
    private readonly SftpReader sut;
    private readonly ISftpWrapper sftpWrapper;
    
    public SftpReaderTests()
    {
        sftpWrapper = A.Fake<ISftpWrapper>();

        sut = new SftpReader(sftpWrapper, new NullLogger<SftpReader>());
    }

    [Fact]
    public async Task ReceiveFile_CorrectlyReturnsStream()
    {
        // Arrange
        var connectionInfo = new ConnectionInfo("dsfgh", "sdh", 
            new NoneAuthenticationMethod("asfgafg"));
        
        // Act
        var outStream = await sut.RetrieveFile(connectionInfo, "some/path");

        // Assert
        outStream.Length.Should().Be(0);

        A.CallTo(() => sftpWrapper.DownloadFile(A<Stream>._, A<string>._, A<ConnectionInfo>._))
            .MustHaveHappened();
    }
    
    [Fact]
    public async Task ReceiveFile_RethrowsException_OnError()
    {
        // Arrange
        var connectionInfo = new ConnectionInfo("dsfgh", "sdh", 
            new NoneAuthenticationMethod("asfgafg"));
        A.CallTo(() => sftpWrapper.DownloadFile(A<Stream>._, 
                A<string>.That.Matches(a => a == "throw"), 
                A<ConnectionInfo>._))
            .Throws<SftpPathNotFoundException>();
        
        // Act
        Func<Task> action = async () => await sut.RetrieveFile(connectionInfo, "throw");

        // Assert
        await action.Should().ThrowAsync<SftpPathNotFoundException>();
    }
}