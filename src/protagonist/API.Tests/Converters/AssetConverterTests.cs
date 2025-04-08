using System;
using System.Collections.Generic;
using API.Converters;
using API.Exceptions;
using DLCS.Core.Types;
using DLCS.HydraModel;
using DLCS.Model.Assets;
using AssetFamily = DLCS.HydraModel.AssetFamily;

namespace API.Tests.Converters;

public class AssetConverterTests
{
    private const string AssetApiId = "https://dlcs.io/customers/1/spaces/99/images/asset";
    private const int Customer = 1;
    
    [Fact]
    public void ToDlcsModel_Fails_IfCustomerNotAsserted()
    {
        var hydraImage = new Image();
        Action action = () => hydraImage.ToDlcsModel(0, 0);
        action.Should().Throw<APIException>();
    }

    [Fact]
    public void ToDlcsModel_ModelId_May_Be_Inferred()
    {
        var hydraImage = new Image{ Id = AssetApiId };
        var asset = hydraImage.ToDlcsModel(1, 99);
        asset.Id.Should().Be(AssetId.FromString($"{Customer}/99/asset"));
    }
    
    [Fact]
    public void ToDlcsModel_HydraId_Must_Be_Correct_Form()
    {
        var hydraImage = new Image{ Id = $"{Customer}/99/asset" };
        Action action = () => hydraImage.ToDlcsModel(1, 99);
        action.Should().Throw<APIException>();
    }
    
    [Fact]
    public void ToDlcsModel_Id_Is_Inferred()
    {
        var hydraImage = new Image{ Space = 99, ModelId = "asset"};
        var asset = hydraImage.ToDlcsModel(1);
        asset.Space.Should().Be(99);
        asset.Id.Should().Be(AssetId.FromString($"{Customer}/99/asset"));
    }

    [Fact]
    public void ToDlcsModel_Space_Assertion_Must_Agree()
    {
        var hydraImage = new Image{ Space = 99, ModelId = "asset"};
        Action action = () => hydraImage.ToDlcsModel(1, 98);
        action.Should().Throw<APIException>();
    }
    
    [Fact]
    public void ToDlcsModel_Id_IfSupplied_MustMatch_Assertions()
    {
        var hydraImage = new Image{ Id = AssetApiId };
        Action action = () => hydraImage.ToDlcsModel(1, 98);
        action.Should().Throw<APIException>();
    }

    [Fact]
    public void ToDlcsModel_All_Id_Parts_Can_be_Provided()
    {
        // This is for a scenario where customer, space and modelId can all be obtained from the path,
        // e.g., a PUT operation
        var hydraImage = new Image();
        var asset = hydraImage.ToDlcsModel(99, 55, "model-id");
        asset.Customer.Should().Be(99);
        asset.Space.Should().Be(55);
        asset.Id.Should().Be(AssetId.FromString("99/55/model-id"));
    }
    
    [Theory]
    [InlineData("https://dlcs.io/thumbnailPolicies/thumb100")]
    [InlineData("thumb100")]
    public void ToDlcsModel_ThumbnailPolicy_CanBeIdOrFull(string policy)
    {
        var hydraImage = new Image { ThumbnailPolicy = policy };
        var asset = hydraImage.ToDlcsModel(99, 55, "model-id");
        asset.ThumbnailPolicy.Should().Be("thumb100");
    }

    [Theory]
    [InlineData("https://dlcs.io/imageOptimisationPolicies/super-max")]
    [InlineData("https://dlcs.io/customer/123/imageOptimisationPolicies/super-max")]
    [InlineData("super-max")]
    public void ToDlcsModel_ImageOptimisationPolicy_CanBeIdOrFull(string policy)
    {
        var hydraImage = new Image { ImageOptimisationPolicy = policy };
        var asset = hydraImage.ToDlcsModel(99, 55, "model-id");
        asset.ImageOptimisationPolicy.Should().Be("super-max");
    }
    
    [Fact]
    public void ToDlcsModel_All_Fields_Should_Convert()
    {
        var created = DateTime.UtcNow.AddDays(-1).Date;
        var queued = created.AddHours(1);
        var dequeued = queued.AddHours(1);
        var finished = dequeued.AddHours(1);
        var origin = "https://example.org/origin";
        var roles = new[] { "role1", "role2" };
        var tags = new[] { "tag1", "tag2" };
        var mediaType = "image/jpeg";
        var thumbnailPolicy = "https://dlcs.io/thumbnailPolicies/thumb100";
        var manifests = new[] { "firstManifest" };
        
        var hydraImage = new Image
        {
            Id = AssetApiId,
            Space = 99,
            Created = created,
            Queued = queued,
            Dequeued = dequeued,
            Finished = finished,
            Width = 1000,
            Height = 2000,
            Duration = 3000,
            Family = AssetFamily.Image,
            Ingesting = true,
            Number1 = 1,
            Number2 = 2,
            Number3 = 3,
            String1 = "1",
            String2 = "2",
            String3 = "3",
            Origin = origin,
            Roles = roles,
            Tags = tags,
            MaxUnauthorised = 400,
            MediaType = mediaType,
            ThumbnailPolicy = thumbnailPolicy,
            Manifests = manifests,
        };

        var asset = hydraImage.ToDlcsModel(1);

        asset.Id.Should().Be(AssetId.FromString($"{Customer}/99/asset"));
        asset.Created.Should().Be(created);
        asset.Finished.Should().Be(finished);
        asset.Customer.Should().Be(Customer);
        asset.Space.Should().Be(99);
        asset.Width.Should().Be(1000);
        asset.Height.Should().Be(2000);
        asset.Duration.Should().Be(3000);
        asset.Family.Should().Be(DLCS.Model.Assets.AssetFamily.Image);
        asset.Ingesting.Should().Be(true);
        asset.Origin.Should().Be(origin);
        asset.NumberReference1.Should().Be(1);
        asset.NumberReference2.Should().Be(2);
        asset.NumberReference3.Should().Be(3);
        asset.Reference1.Should().Be("1");
        asset.Reference2.Should().Be("2");
        asset.Reference3.Should().Be("3");
        asset.Roles.Split(',').Should().BeEquivalentTo(roles);
        asset.Tags.Split(',').Should().BeEquivalentTo(tags);
        asset.DeliveryChannels.Should().BeEmpty();
        asset.MaxUnauthorised.Should().Be(400);
        asset.MediaType.Should().Be(mediaType);
        asset.ThumbnailPolicy.Should().Be("thumb100");
        asset.Manifests.Should().BeEquivalentTo(manifests);
    }
    
    [Fact]
    public void ToHydraModel_All_Fields_Should_Convert()
    {
        var created = DateTime.UtcNow.AddDays(-1).Date;
        var finished = DateTime.UtcNow;
        var origin = "https://example.org/origin";
        var roles = "role1,role2";
        var tags = "tag1tag2";
        var mediaType = "image/jpeg";
        var thumbnailPolicy = "thumb100";
        var manifests = new List<string> { "firstManifest" };
        
        var asset = new Asset()
        {
            Id = AssetId.FromString($"{Customer}/99/asset"),
            Customer = 1,
            Space = 99,
            Created = created,
            Finished = finished,
            Width = 1000,
            Height = 2000,
            Duration = 3000,
            Family = DLCS.Model.Assets.AssetFamily.Image,
            Ingesting = true,
            NumberReference1 = 1,
            NumberReference2 = 2,
            NumberReference3 = 3,
            Reference1 = "1",
            Reference2 = "2",
            Reference3 = "3",
            Origin = origin,
            Roles = roles,
            Tags = tags,
            MaxUnauthorised = 400,
            MediaType = mediaType,
            ThumbnailPolicy = thumbnailPolicy,
            Manifests = manifests,
        };

        var hydraImage = asset.ToHydra(new UrlRoots()
        {
            BaseUrl = "https://dlcs.io"
        });

        hydraImage.Id.Should().Be(AssetApiId);
        hydraImage.Created.Should().Be(created);
        hydraImage.Finished.Should().Be(finished);
        hydraImage.CustomerId.Should().Be(1);
        hydraImage.Space.Should().Be(99);
        hydraImage.Width.Should().Be(1000);
        hydraImage.Height.Should().Be(2000);
        hydraImage.Duration.Should().Be(3000);
        hydraImage.Family.Should().Be(AssetFamily.Image);
        hydraImage.Ingesting.Should().Be(true);
        hydraImage.Origin.Should().Be(origin);
        hydraImage.Number1.Should().Be(1);
        hydraImage.Number2.Should().Be(2);
        hydraImage.Number3.Should().Be(3);
        hydraImage.String1.Should().Be("1");
        hydraImage.String2.Should().Be("2");
        hydraImage.String3.Should().Be("3");
        hydraImage.Roles.Should().BeEquivalentTo(roles.Split(','));
        hydraImage.Tags.Should().BeEquivalentTo(tags.Split(','));
        hydraImage.DeliveryChannels.Should().BeEmpty();
        hydraImage.MaxUnauthorised.Should().Be(400);
        hydraImage.MediaType.Should().Be(mediaType);
        hydraImage.ThumbnailPolicy.Should().Be($"https://dlcs.io/thumbnailPolicies/{thumbnailPolicy}");
        hydraImage.Manifests.Should().BeEquivalentTo(manifests);
    }
}
