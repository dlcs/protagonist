using System.Collections.Generic;
using IIIF.ImageApi.V3;
using Orchestrator.Features.Images.ImageServer;

namespace Orchestrator.Tests.Features.Images;

// v3 info.json lists 'sizeUpscaling' exactly when OrchestratorSettings.AllowUpscaling is true, whatever the image server's
// own info.json listed (Cantaloupe may list it). ADR 0011, 2026-11-24.
public class InfoJson3ConstructorTests
{
    [Fact]
    public void SetSizeUpscaling_RemovesTheImageServersListing_IfUpscalingNotAllowed()
    {
        var service = new ImageService3 { ExtraFeatures = new List<string> { "cors", "sizeUpscaling", "mirroring" } };
        InfoJson3Constructor.SetSizeUpscaling(service, false);
        service.ExtraFeatures.Should().BeEquivalentTo("cors", "mirroring");
    }

    [Fact]
    public void SetSizeUpscaling_LeavesNoEmptyList_IfOnlyFeatureRemoved()
    {
        var service = new ImageService3 { ExtraFeatures = new List<string> { "sizeUpscaling" } };
        InfoJson3Constructor.SetSizeUpscaling(service, false);
        service.ExtraFeatures.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("cors")]
    [InlineData("sizeUpscaling")]
    public void SetSizeUpscaling_ListsItOnce_IfUpscalingAllowed(string? existing)
    {
        var service = new ImageService3 { ExtraFeatures = existing == null ? null : new List<string> { existing } };
        InfoJson3Constructor.SetSizeUpscaling(service, true);
        service.ExtraFeatures.Should().ContainSingle(f => f == "sizeUpscaling");
    }
}
