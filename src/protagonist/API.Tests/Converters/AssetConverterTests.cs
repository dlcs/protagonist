using System;
using API.Converters;
using API.Exceptions;
using DLCS.Core.Types;
using DLCS.HydraModel;

namespace API.Tests.Converters;

public class AssetConverterTests
{
    private const string AssetApiId = "https://dlcs.io/customers/1/spaces/99/images/asset";
    
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
        asset.Id.Should().Be(AssetId.FromString("1/99/asset"));
    }
    
    [Fact]
    public void ToDlcsModel_HydraId_Must_Be_Correct_Form()
    {
        var hydraImage = new Image{ Id = "1/99/asset" };
        Action action = () => hydraImage.ToDlcsModel(1, 99);
        action.Should().Throw<APIException>();
    }
    
    [Fact]
    public void ToDlcsModel_Id_Is_Inferred()
    {
        var hydraImage = new Image{ Space = 99, ModelId = "asset"};
        var asset = hydraImage.ToDlcsModel(1);
        asset.Space.Should().Be(99);
        asset.Id.Should().Be(AssetId.FromString("1/99/asset"));
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
        var initialOrigin = "https://example.org/initial-origin";
        var origin = "https://example.org/origin";
        var roles = new[] { "role1", "role2" };
        var tags = new[] { "tag1", "tag2" };
        var deliveryChannel = new[] { "iiif-av", "iiif-img" };
        var mediaType = "image/jpeg";
        var thumbnailPolicy = "https://dlcs.io/thumbnailPolicies/thumb100";
        
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
            InitialOrigin = initialOrigin,
            Origin = origin,
            Roles = roles,
            Tags = tags,
            MaxUnauthorised = 400,
            MediaType = mediaType,
            ThumbnailPolicy = thumbnailPolicy,
            DeliveryChannels = deliveryChannel
        };

        var asset = hydraImage.ToDlcsModel(1);

        asset.Id.Should().Be(AssetId.FromString("1/99/asset"));
        asset.Created.Should().Be(created);
        asset.Finished.Should().Be(finished);
        asset.Customer.Should().Be(1);
        asset.Space.Should().Be(99);
        asset.Width.Should().Be(1000);
        asset.Height.Should().Be(2000);
        asset.Duration.Should().Be(3000);
        asset.Family.Should().Be(DLCS.Model.Assets.AssetFamily.Image);
        asset.Ingesting.Should().Be(true);
        asset.Origin.Should().Be(origin);
        asset.InitialOrigin.Should().Be(initialOrigin); // not patchable but still carried on the Asset class.
        asset.NumberReference1.Should().Be(1);
        asset.NumberReference2.Should().Be(2);
        asset.NumberReference3.Should().Be(3);
        asset.Reference1.Should().Be("1");
        asset.Reference2.Should().Be("2");
        asset.Reference3.Should().Be("3");
        asset.Roles.Split(',').Should().BeEquivalentTo(roles);
        asset.Tags.Split(',').Should().BeEquivalentTo(tags);
        asset.DeliveryChannels.Should().BeEquivalentTo(deliveryChannel);
        asset.MaxUnauthorised.Should().Be(400);
        asset.MediaType.Should().Be(mediaType);
        asset.ThumbnailPolicy.Should().Be("thumb100");
    }

    [Fact]
    public void ToDlcsModel_ReordersDeliveryChannel()
    {
        // Arrange
        var deliveryChannel = new[] { "iiif-img", "file", "iiif-av" };

        var hydraImage = new Image
        {
            Id = AssetApiId,
            Space = 99,
            DeliveryChannels = deliveryChannel
        };
        
        // Act
        var asset = hydraImage.ToDlcsModel(1);
        
        // Assert
        asset.DeliveryChannels.Should().BeInAscendingOrder();
    }
}