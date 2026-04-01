using System;
using API.Converters;
using API.Exceptions;
using DLCS.Core.Types;
using DLCS.Model.Assets;
using Adjunct = DLCS.HydraModel.Adjunct;

namespace API.Tests.Converters;

public class AdjunctConverterTests
{
    private const int Customer = 99;
    private const int Space = 1;
    private const string AssetId = "test-asset";
    private const string AdjunctId = "test-adjunct";
    private const string BaseUrl = "https://dlcs.example";

    // Tests for ToDlcsModel(hydraAdjunct, customerId) — parses Asset field internally

    [Fact]
    public void ToDlcsModel_ParseAssetId_ShortFormAsset_ConvertsCorrectly()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Asset = $"{Customer}/{Space}/{AssetId}",
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer);

        // Assert
        adjunct.AssetId.Should().Be(new AssetId(Customer, Space, AssetId));
        adjunct.Id.Should().Be(AdjunctId);
    }

    [Fact]
    public void ToDlcsModel_ParseAssetId_FullUriAsset_ConvertsCorrectly()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Asset = $"https://dlcs.example/customers/{Customer}/spaces/{Space}/images/{AssetId}",
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer);

        // Assert
        adjunct.AssetId.Should().Be(new AssetId(Customer, Space, AssetId));
        adjunct.Id.Should().Be(AdjunctId);
    }

    [Fact]
    public void ToDlcsModel_ParseAssetId_UsesSuppliedAdjunctId()
    {
        // Arrange
        const string suppliedId = "my-supplied-id";
        var hydraAdjunct = new Adjunct
        {
            ModelId = "original-id",
            Asset = $"{Customer}/{Space}/{AssetId}",
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, suppliedId);

        // Assert
        adjunct.Id.Should().Be(suppliedId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-asset-ref")]
    [InlineData("https://example.com/no/asset/path/here")]
    public void ToDlcsModel_ParseAssetId_ThrowsBadRequestException_WhenAssetUnparseable(string asset)
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Asset = asset,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act & Assert
        var action = () => hydraAdjunct.ToDlcsModel(Customer);
        action.Should().Throw<BadRequestException>();
    }

    [Fact]
    public void ToDlcsModel_ParseAssetId_ThrowsBadRequestException_WhenAssetBelongsToDifferentCustomer()
    {
        // Arrange
        const int differentCustomer = 1;
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Asset = $"{Customer}/{Space}/{AssetId}", // Customer 99, not 1
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act & Assert
        var action = () => hydraAdjunct.ToDlcsModel(differentCustomer);
        action.Should().Throw<BadRequestException>()
            .WithMessage($"Asset '{Customer}/{Space}/{AssetId}' does not belong to customer {differentCustomer}");
    }

    [Fact]
    public void ToDlcsModel_WithOrigin()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            Profile = "https://profile.example/1",
            Label = new() { { "en", ["Test Label"] } },
            Language = ["en"],
            Origin = "https://example.com/adjunct",
            Motivation = "sc:supplementing",
            Size = 1024
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, Space, AssetId, AdjunctId);

        // Assert
        adjunct.Id.Should().Be(AdjunctId);
        adjunct.Type.Should().Be("AnnotationPage");
        adjunct.MediaType.Should().Be("application/json");
        adjunct.IIIFLink.Should().Be(IIIFLinkType.SeeAlso);
        adjunct.Profile.Should().Be("https://profile.example/1");
        adjunct.Label.Should().ContainKey("en");
        adjunct.Language.Should().Equal("en");
        adjunct.Origin.Should().Be("https://example.com/adjunct");
        adjunct.Motivation.Should().Be("sc:supplementing");
        adjunct.Size.Should().Be(1024);
        adjunct.AssetId.Should().Be(new AssetId(Customer, Space, AssetId));
        adjunct.ExternalId.Should().BeNull();
    }

    [Fact]
    public void ToDlcsModel_WithExternalId()
    {
        // Arrange
        const string externalId = "https://example.com/external-adjunct";
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "rendering",
            ExternalId = externalId,
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, Space, AssetId);

        // Assert
        adjunct.Id.Should().Be(AdjunctId);
        adjunct.ExternalId.Should().Be(new Uri(externalId));
        adjunct.Origin.Should().BeNull();
        adjunct.IIIFLink.Should().Be(IIIFLinkType.Rendering);
    }

    [Fact]
    public void ToDlcsModel_WithInlineAnnotationIIIFLink()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "inlineAnnotation",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, Space, AssetId);

        // Assert
        adjunct.IIIFLink.Should().Be(IIIFLinkType.InlineAnnotation);
    }

    [Fact]
    public void ToDlcsModel_WithAnnotationsIIIFLink()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "annotations",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, Space, AssetId);

        // Assert
        adjunct.IIIFLink.Should().Be(IIIFLinkType.Annotations);
    }

    [Fact]
    public void ToDlcsModel_ThrowsApiException_IfIIIFLinkInvalid()
    {
        // Arrange
        var hydraAdjunct = new Adjunct
        {
            ModelId = AdjunctId,
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "invalidLink",
            ExternalId = "https://example.com/adjunct",
        };

        // Act & Assert
        var action = () => hydraAdjunct.ToDlcsModel(Customer, Space, AssetId);
        action.Should().Throw<APIException>().WithMessage("Hydra adjunct 'iiifLink' could not be parsed");
    }

    [Fact]
    public void ToDlcsModel_UsesSuppliedAdjunctId_IfProvided()
    {
        // Arrange
        const string suppliedAdjunctId = "supplied-id";
        var hydraAdjunct = new Adjunct
        {
            ModelId = "original-id",
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = "seeAlso",
            ExternalId = "https://example.com/adjunct",
        };

        // Act
        var adjunct = hydraAdjunct.ToDlcsModel(Customer, Space, AssetId, suppliedAdjunctId);

        // Assert
        adjunct.Id.Should().Be(suppliedAdjunctId);
    }

    [Fact]
    public void ToHydra_ConvertsAllFields()
    {
        // Arrange
        var created = DateTime.UtcNow.AddDays(-5);
        var finished = DateTime.UtcNow;
        var externalIdUri = new Uri("https://example.com/external-adjunct");

        var domain = new DLCS.Model.Assets.Adjunct
        {
            Id = AdjunctId,
            AssetId = new AssetId(Customer, Space, AssetId),
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = IIIFLinkType.SeeAlso,
            Profile = "https://profile.example/1",
            Label = new() { { "en", ["Test Label"] } },
            Language = ["en"],
            ExternalId = externalIdUri,
            Origin = null,
            Size = 2048,
            Created = created,
            Finished = finished,
            Motivation = "sc:supplementing",
            Ingesting = true,
            Error = null,
        };

        // Act
        var hydra = domain.ToHydra(new UrlRoots { BaseUrl = BaseUrl });

        // Assert
        hydra.ModelId.Should().Be(AdjunctId);
        hydra.Type.Should().Be("AnnotationPage");
        hydra.MediaType.Should().Be("application/json");
        hydra.IIIFLink.Should().Be("seeAlso");
        hydra.Profile.Should().Be("https://profile.example/1");
        hydra.Label.Should().ContainKey("en");
        hydra.Language.Should().Equal("en");
        hydra.ExternalId.Should().Be("https://example.com/external-adjunct");
        hydra.Origin.Should().BeNull();
        hydra.Size.Should().Be(2048);
        hydra.Created.Should().Be(created);
        hydra.Finished.Should().Be(finished);
        hydra.Motivation.Should().Be("sc:supplementing");
        hydra.Ingesting.Should().BeTrue();
        hydra.Error.Should().BeNull();
        hydra.Asset.Should().Be($"{BaseUrl}/customers/{Customer}/spaces/{Space}/images/{AssetId}");
        hydra.PublicId.Should().Be("https://example.com/external-adjunct");
        hydra.Batch.Should().BeNull();
    }

    [Fact]
    public void ToHydra_WithOrigin_SetsPublicId()
    {
        // Arrange
        var domain = new DLCS.Model.Assets.Adjunct
        {
            Id = AdjunctId,
            AssetId = new AssetId(Customer, Space, AssetId),
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = IIIFLinkType.SeeAlso,
            Origin = "https://example.com/origin",
            ExternalId = null,
        };

        // Act
        var hydra = domain.ToHydra(new UrlRoots { BaseUrl = BaseUrl, ResourceRoot = "https://dlcs.orch/" });

        // Assert
        hydra.Origin.Should().Be("https://example.com/origin");
        hydra.ExternalId.Should().BeNull();
        hydra.PublicId.Should().Be("https://dlcs.orch/adjuncts/99/1/test-asset/test-adjunct");
    }

    [Fact]
    public void ToHydra_IdPropertyMatchesExpectedFormat()
    {
        // Arrange
        var domain = new DLCS.Model.Assets.Adjunct
        {
            Id = AdjunctId,
            AssetId = new AssetId(Customer, Space, AssetId),
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = IIIFLinkType.SeeAlso,
            Origin = "https://example.com/origin",
        };

        // Act
        var hydra = domain.ToHydra(new UrlRoots { BaseUrl = BaseUrl });

        // Assert
        // Adjunct @id should follow pattern: /customers/{customer}/spaces/{space}/images/{asset}/adjuncts/{adjunct}
        hydra.Id.Should().Be($"{BaseUrl}/customers/{Customer}/spaces/{Space}/images/{AssetId}/adjuncts/{AdjunctId}");
    }
    
    [Fact]
    public void ToHydra_WithBatch_SetsBatchUri()
    {
        // Arrange
        var domain = new DLCS.Model.Assets.Adjunct
        {
            Id = AdjunctId,
            AssetId = new AssetId(Customer, Space, AssetId),
            Type = "AnnotationPage",
            MediaType = "application/json",
            IIIFLink = IIIFLinkType.SeeAlso,
            Batch = 4567,
        };

        // Act
        var hydra = domain.ToHydra(new UrlRoots { BaseUrl = BaseUrl, ResourceRoot = "https://dlcs.orch/" });

        // Assert
        hydra.Batch.Should().Be("https://dlcs.example/customers/99/adjunctQueue/batches/4567");
    }
}
