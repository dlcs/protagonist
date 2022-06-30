using System;
using API.Converters;
using DLCS.HydraModel;
using FluentAssertions;
using Xunit;

namespace API.Tests.Assets;

public class ConverterTests
{
    private const string AssetId = "https://dlcs.io/customers/1/spaces/99/images/asset";
    
    [Fact]
    public void ConversionToModel_Fails_IfCustomerNotAsserted()
    {
        var hydraImage = new Image();
        Action action = () => hydraImage.ToDlcsModel(0, 0);
        action.Should().Throw<APIException>();
    }
    
    
    [Fact]
    public void ModelId_May_Be_Inferred()
    {
        var hydraImage = new Image{ Id = AssetId };
        var asset = hydraImage.ToDlcsModel(1, 99);
        asset.Id.Should().Be("1/99/asset");
    }
    
    [Fact]
    public void HydraId_Must_Be_Correct_Form()
    {
        var hydraImage = new Image{ Id = "1/99/asset" };
        Action action = () => hydraImage.ToDlcsModel(1, 99);
        action.Should().Throw<APIException>();
    }
    
    [Fact]
    public void Id_May_Be_Inferred()
    {
        var hydraImage = new Image{ Space = 99, ModelId = "asset"};
        var asset = hydraImage.ToDlcsModel(1);
        asset.Space.Should().Be(99);
        asset.Id.Should().Be("1/99/asset");
    }
    
    
    [Fact]
    public void Space_Assertion_Must_Agree()
    {
        var hydraImage = new Image{ Space = 99, ModelId = "asset"};
        Action action = () => hydraImage.ToDlcsModel(1, 98);
        action.Should().Throw<APIException>();
    }
    
    
    [Fact]
    public void Id_IfSupplied_MustMatch_Assertions()
    {
        var hydraImage = new Image{ Id = AssetId };
        Action action = () => hydraImage.ToDlcsModel(1, 98);
        action.Should().Throw<APIException>();
    }

    [Fact]
    public void All_Id_Parts_Can_be_Provided()
    {
        // This is for a scenario where customer, space and modelId can all be obtained from the path,
        // e.g., a PUT operation
        var hydraImage = new Image();
        var asset = hydraImage.ToDlcsModel(99, 55, "model-id");
        asset.Customer.Should().Be(99);
        asset.Space.Should().Be(55);
        asset.Id.Should().Be("99/55/model-id");
    }
    
    // Now do Model -> Hydra tests

    [Fact]
    public void All_Fields_Should_Convert()
    {
        var created = DateTime.UtcNow.AddDays(-1).Date;
        var queued = created.AddHours(1);
        var dequeued = queued.AddHours(1);
        var finished = dequeued.AddHours(1);
        var initialOrigin = "https://example.org/initial-origin";
        var origin = "https://example.org/origin";
        var roles = new[] { "role1", "role2" };
        var tags = new[] { "tag1", "tag2" };
        var mediaType = "image/jpeg";
        var thumbnailPolicy = "https://dlcs.io/thumbnailPolicies/thumb100";
        
        var hydraImage = new Image
        {
            Id = AssetId,
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
            ThumbnailPolicy = thumbnailPolicy
        };

        var asset = hydraImage.ToDlcsModel(1);

        asset.Id.Should().Be("1/99/asset");
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
        asset.InitialOrigin.Should().BeNull(); // not patchable
        asset.NumberReference1.Should().Be(1);
        asset.NumberReference2.Should().Be(2);
        asset.NumberReference3.Should().Be(3);
        asset.Reference1.Should().Be("1");
        asset.Reference2.Should().Be("2");
        asset.Reference3.Should().Be("3");
        asset.Roles.Split(',').Should().BeEquivalentTo(roles);
        asset.Tags.Split(',').Should().BeEquivalentTo(tags);
        asset.MaxUnauthorised.Should().Be(400);
        asset.MediaType.Should().Be(mediaType);
        asset.ThumbnailPolicy.Should().Be("thumb100");

    }
}