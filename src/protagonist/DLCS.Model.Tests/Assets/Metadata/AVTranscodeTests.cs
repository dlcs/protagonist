using System;
using DLCS.Model.Assets.Metadata;

namespace DLCS.Model.Tests.Assets.Metadata;

public class AVTranscodeTests
{
    [Fact]
    public void GetTranscodeRequestPath_Correct_Video()
    {
        var avTranscode = new AVTranscode
        {
            Location = new Uri("s3://dlcs-storage/1/2/identity_of_asset/full/full/max/max/0/default.webm")
        };

        avTranscode.GetTranscodeRequestPath().Should().Be("full/full/max/max/0/default.webm");
    }
    
    [Fact]
    public void GetTranscodeRequestPath_Correct_Audio()
    {
        var avTranscode = new AVTranscode
        {
            Location = new Uri("s3://dlcs-storage/1/2/identity_of_asset/full/max/default.mp3")
        };

        avTranscode.GetTranscodeRequestPath().Should().Be("full/max/default.mp3");
    }
}
