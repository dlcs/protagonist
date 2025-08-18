using DLCS.Core.Types;
using DLCS.Model.Messaging;

namespace DLCS.Model.Tests.Messaging;

public class IngestAssetRequestTests
{
    [Theory]
    [InlineData(123, 123)]
    [InlineData(0, null)]
    [InlineData(null, null)]
    public void Ctor_SetsBatchIdCorrectly(int? input, int? expected)
    {
        var request = new IngestAssetRequest(new AssetId(1, 2, "foo"), null, input);
        
        request.BatchId.Should().Be(expected);
    }
}