using System.Collections.Generic;
using System.Net;
using System.Threading;
using DLCS.Core.Exceptions;
using DLCS.Core.Settings;
using DLCS.Core.Types;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using IIIF;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orchestrator.Assets;
using Orchestrator.Features.Images;
using Orchestrator.Infrastructure;
using Orchestrator.Infrastructure.Auth;
using Orchestrator.Infrastructure.ReverseProxy;
using Orchestrator.Settings;
using Test.Helpers.Data;
using Version = IIIF.ImageApi.Version;

namespace Orchestrator.Tests.Features.Images;

public class ImageRequestHandlerTests
{
    private readonly IAssetTracker assetTracker;
    private readonly IAssetDeliveryPathParser assetDeliveryPathParser;
    private readonly IPathCustomerRepository customerRepository;
    private readonly AssetDeliveryPathParser assetDeliveryPathParserImpl;
    private readonly IAssetAccessValidator accessValidator;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ICustomHeaderRepository customHeaderRepository;
    private static readonly int[] Item = [256, 256];

    public ImageRequestHandlerTests()
    {
        assetTracker = A.Fake<IAssetTracker>();
        assetDeliveryPathParser = A.Fake<IAssetDeliveryPathParser>();
        customerRepository = A.Fake<IPathCustomerRepository>();
        accessValidator = A.Fake<IAssetAccessValidator>();
        assetDeliveryPathParserImpl = new AssetDeliveryPathParser(customerRepository, new NullLogger<AssetDeliveryPathParser>());
        customHeaderRepository = A.Fake<ICustomHeaderRepository>();

        scopeFactory = A.Fake<IServiceScopeFactory>();
        var scope = A.Fake<IServiceScope>();
        A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
        A.CallTo(() => scope.ServiceProvider.GetService(typeof(IAssetAccessValidator))).Returns(accessValidator);
    }

    private static OrchestratorSettings CreateOrchestratorSettings() =>
        new()
        {
            Proxy = new(),
            ImageServerPathConfig = new Dictionary<ImageServer, ImageServerConfig>
            {
                [ImageServer.Cantaloupe] = new()
                {
                    Separator = "%2F",
                    PathTemplate = "/path",
                    VersionPathTemplates = new Dictionary<Version, string>
                    {
                        [Version.V3] = "cantaloupe-3",
                        [Version.V2] = "cantaloupe-2"
                    }
                },
                [ImageServer.IIPImage] = new()
                {
                    Separator = "/",
                    PathTemplate = "/path",
                    VersionPathTemplates = new Dictionary<Version, string>
                    {
                        [Version.V2] = "iip"
                    }
                }
            }
        };

    [Fact]
    public async Task HandleRequest_Returns404_IfAssetPathParserThrowsHttpException_NotFound()
    {
        // Arrange
        A.CallTo(() => assetDeliveryPathParser.ParseForHttp<ImageAssetDeliveryRequest>(A<string>._))
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, "Could not find Customer/Space"));
        var sut = GetImageRequestHandlerWithMockPathParser(true);
            
        // Act
        var result = await sut.HandleRequest(new DefaultHttpContext());
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Fact]
    public async Task HandleRequest_Returns400_IfAssetPathParserThrowsHttpException_BadRequest()
    {
        // NOTE - routes should prevent this from ever happening
            
        // Arrange
        A.CallTo(() => assetDeliveryPathParser.ParseForHttp<ImageAssetDeliveryRequest>(A<string>._))
            .ThrowsAsync(new HttpException(HttpStatusCode.BadRequest, "Error parsing path"));
        var sut = GetImageRequestHandlerWithMockPathParser(true);
            
        // Act
        var result = await sut.HandleRequest(new DefaultHttpContext());
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task HandleRequest_Returns400_IfUnableToDetermineImageVersion()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = context.Request.Path = "/iiif-img/v4/2/2/test-image/full/full/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = new List<int[]>(), S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });
        
        var sut = GetImageRequestHandlerWithMockPathParser();
            
        // Act
        var result = await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("full/0,/0/default.jpg")] // size
    [InlineData("0,0,512,512/,0/0/default.jpg")] // size
    [InlineData("square/!0,0/0/default.jpg")] // size
    [InlineData("full/20,0/0/default.jpg")] // size
    [InlineData("full/0,20/0/default.jpg")] // size
    [InlineData("full/max/0/vibrant.jpg")] // quality
    [InlineData("full/max/0/default.pdf")] // format
    public async Task HandleRequest_Returns400_IfInvalidRequest(string imageRequest)
    {
        // Arrange
        var id = AssetIdGenerator.GetAssetId();

        // Act
        var context = new DefaultHttpContext();
        context.Request.Path = $"/iiif-img/{id}/{imageRequest}";

        var sut = GetImageRequestHandlerWithMockPathParser();
            
        // Act
        var result = await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData(AvailableDeliveryChannel.File)]
    [InlineData(AvailableDeliveryChannel.Timebased)]
    [InlineData(AvailableDeliveryChannel.File | AvailableDeliveryChannel.Timebased)]
    public async Task HandleRequest_Returns404_IfAssetDoesNotHaveImageDeliveryChannel(AvailableDeliveryChannel deliveryChannel)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/!200,200/0/default.jpg";
        var sut = GetImageRequestHandlerWithMockPathParser();
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(new AssetId(2, 2, "test-image")))
            .Returns(new OrchestrationImage { Channels = deliveryChannel, RequiresAuth = true});
            
        // Act
        var result = await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("full/501,")]
    [InlineData("full/,501")]
    [InlineData("square/pct:51,")]
    [InlineData("0,0,512,512/512,")]
    public async Task HandleRequest_Returns403_IfRequestedExceedsMaxWidth(string sizeAndRegion)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = $"/iiif-img/2/2/test-image/{sizeAndRegion}/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(new AssetId(2, 2, "test-image")))
            .Returns(new OrchestrationImage
            {
                Size = new Size(1000, 1000), MaxWidth = 500, 
                Channels = AvailableDeliveryChannel.Image, S3Location = "s3://"
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task HandleRequest_Returns401_IfAssetRequiresAuth_AndUserCannotAccess()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/!200,200/0/default.jpg";

        var roles = new List<string> { "role" };
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(new AssetId(2, 2, "test-image")))
            .Returns(new OrchestrationImage
            {
                Size = new Size(1000, 1000), MaxWidth = 5000, Roles = roles, RequiresAuth = true, 
                Channels = AvailableDeliveryChannel.Image, S3Location = "s3://"
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(AssetAccessResult.Unauthorized);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("900,")]
    [InlineData("900,900")]
    [InlineData(",900")]
    [InlineData("!900,900")]
    [InlineData("pct:50")]
    public async Task HandleRequest_ProxiesToSpecialServer_IfAssetRequiresAuth_AndUserNotAuthorised_ButFullRequest_SizeEqualToOpenFullMax(
        string sizeParameter)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = $"/iiif-img/2/2/test-image/full/{sizeParameter}/0/default.jpg";

        var roles = new List<string> { "role" };
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Roles = roles, OpenThumbs = [[150, 150]], MaxWidth = 5000,
                OpenFullMax = 900, Size = new Size(1800, 1800), RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);

        // Assert
        result!.Target.Should().Be(ProxyDestination.SpecialServer);
        result.HasPath.Should().BeTrue();
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).MustNotHaveHappened();
    }
    
    [Theory]
    [InlineData("/full/900,/")]
    [InlineData("/full/,900/")]
    [InlineData("/full/!900,900/")]
    [InlineData("/0,0,900,900/900,/")]
    [InlineData("/0,0,900,900/,900/")]
    [InlineData("/0,0,900,900/!900,900/")]
    [InlineData("/square/900,/")]
    [InlineData("/square/,900/")]
    [InlineData("/square/!900,900/")]
    public async Task HandleRequest_ProxiesToSpecialServer_IfAssetRequiresAuth_AndUserNotAuthorised_ButFullOrEquivalentRequest_SizeEqualToOpenFullMax(string iiifRequest)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = $"/iiif-img/2/2/test-image{iiifRequest}0/default.jpg";

        var roles = new List<string> { "role" };
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Roles = roles, OpenThumbs = [[150, 150]], MaxWidth = 5000,
                OpenFullMax = 900, Size = new Size(900, 900), RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);

        // Assert
        result.Target.Should().Be(ProxyDestination.SpecialServer);
        result.HasPath.Should().BeTrue();
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("/full/901,901/", "Size too large")]
    [InlineData("/full/max/", "Max size")]
    [InlineData("/0,0,900,900/900,/", "Tiled region")]
    [InlineData("/pct:0,0,512,512/!10,10/", "Percent region")]
    [InlineData("/square/!901,901/", "Square region too large")]
    public async Task HandleRequest_Returns401_IfAssetRequiresAuth_AndUserNotAuthorised_AndRequestNotForOpenFullMax(
        string iiifRequest, string reason)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = $"/iiif-img/2/2/test-image{iiifRequest}0/default.jpg";

        var roles = new List<string> { "role" };
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(new AssetId(2, 2, "test-image")))
            .Returns(new OrchestrationImage
            {
                Roles = roles, OpenFullMax = 900, Size = new Size(1800, 1800), RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(AssetAccessResult.Unauthorized);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context);

        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized, reason);
    }

    [Fact]
    public async Task HandleRequest_ProxiesToThumbs_IfRequiresAuth_AndFullRegionOfKnownSize_SmallerThanOpenFullMax()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/!150,150/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[150, 150]], Size = new Size(1000, 1000),
                RequiresAuth = true, Roles = ["role"], OpenFullMax = 200, MaxWidth = 5000, 
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/full/150,150/0/default.jpg");
    }

    [Fact]
    public async Task HandleRequest_ProxiesToThumbs_IfFullRegion_AndKnownSize()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/!150,150/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[150, 150]], Size = new Size(1000, 1000), MaxWidth = 5000,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/full/150,150/0/default.jpg");
    }
    
    [Fact]
    public async Task HandleRequest_ProxiesToThumbs_IfRegionEquivalentToFull_AndKnownSize()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/0,0,512,512/256,256/0/default.jpg";
        
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [Item], MaxWidth = 5000,
                Size = new Size(512, 512), S3Location = "s3://storage/2/2/test-image", 
                Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/0,0,512,512/256,256/0/default.jpg");
    }
    
    [Fact]
    public async Task HandleRequest_ProxiesToThumbs_IfRegionAndOriginSquare_AndKnownSize()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/square/256,256/0/default.jpg";
        
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[256, 256]], MaxWidth = 5000,
                Size = new Size(512, 512), S3Location = "s3://storage/2/2/test-image", 
                Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/square/256,256/0/default.jpg");
    }
    
    [Theory]
    [InlineData(AssetAccessResult.Open)]
    [InlineData(AssetAccessResult.Authorized)]
    public async Task HandleRequest_ProxiesToImageServer_IfFullRegion_AndNoKnownThumb_ButNoS3Location_AndWillReingest(
        AssetAccessResult accessResult)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/,900/0/default.jpg";

        var roles = new List<string> { "role" };
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Roles = roles, OpenThumbs = [[150, 150]], MaxWidth = 5000,
                RequiresAuth = true, Size = new Size(1000, 1000), OpenFullMax = 300,
                Channels = AvailableDeliveryChannel.Image, Reingest = true
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(accessResult);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (ProxyImageServerResult)await sut.HandleRequest(context);

        // Assert
        result.Target.Should().Be(ProxyDestination.ImageServer);
        result.HasPath.Should().BeTrue();
    }
    
    [Theory]
    [InlineData(AssetAccessResult.Open)]
    [InlineData(AssetAccessResult.Authorized)]
    public async Task HandleRequest_ReturnsNotFound_IfFullRegion_AndNoKnownThumb_NoS3Location_AndNotReingest(
        AssetAccessResult accessResult)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/,900/0/default.jpg";

        var roles = new List<string> { "role" };
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Roles = roles, OpenThumbs = [[150, 150]], MaxWidth = 5000,
                RequiresAuth = true, Size = new Size(1000, 1000), OpenFullMax = 300,
                Channels = AvailableDeliveryChannel.Image, Reingest = false
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(accessResult);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = (StatusCodeResult)await sut.HandleRequest(context);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg", "full/90,/0/default.jpg", false)] // full/<size>
    [InlineData("/iiif-img/v2/2/2/test-image/full/full/0/default.jpg", "full/1000,1000/0/default.jpg", true)] // /full/full - v2
    [InlineData("/iiif-img/v2/2/2/test-image/full/max/0/default.jpg", "full/5000,5000/0/default.jpg", true)] // /full/max - v2
    [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg", "full/1000,1000/0/default.jpg", false)] // /full/max - v3
    [InlineData("/iiif-img/2/2/test-image/full/^max/0/default.jpg", "full/^5000,5000/0/default.jpg", false)] // /full/^max - v3
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/default.png", "full/100,100/0/default.png", false)] // png
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/default.tif", "full/100,100/0/default.tif", false)] // tif
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/90/default.jpg", "full/100,100/90/default.jpg", false)] // rotation
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/!0/default.jpg", "full/100,100/!0/default.jpg", false)] // rotation / mirrored
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/bitonal.jpg", "full/100,100/0/bitonal.jpg", false)] // bitonal
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/gray.jpg", "full/100,100/0/gray.jpg", false)] // gray
    [InlineData("/iiif-img/2/2/test-image/square/!100,150/0/bitonal.tif", "square/100,100/0/bitonal.tif", false)] // square
    [InlineData("/iiif-img/2/2/test-image/square/!5000,5000/0/bitonal.tif", "square/1000,1000/0/bitonal.tif", false)] // confined larger than maxWidth
    [InlineData("/iiif-img/v2/2/2/test-image/square/!5000,5000/0/bitonal.tif", "square/5000,5000/0/bitonal.tif", true)] // confined larger than maxWidth v2
    [InlineData("/iiif-img/2/2/test-image/square/^!6000,6000/0/bitonal.tif", "square/^5000,5000/0/bitonal.tif", false)] // confined larger than maxWidth v2
    public async Task HandleRequest_ProxiesToSpecialServer_ForAllLargeFull_RewritingIfRequired(string path, string expectedProxyPath, bool version2)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[150, 150]], S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1000),
            });
        
        var destination = version2 ? "cantaloupe-2" : "cantaloupe-3";

        var expected = $"{destination}s3:%2F%2Fstorage%2F2%2F2%2Ftest-image/{expectedProxyPath}";

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.SpecialServer);
        result.Path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg", "/2/2/test-image/full/90,/0/default.jpg", ProxyDestination.ResizeThumbs)] // full/<size>
    [InlineData("/iiif-img/v2/2/2/test-image/full/full/0/default.jpg", "/v2/2/2/test-image/full/400,400/0/default.jpg", ProxyDestination.Thumbs)] // /full/full - v2
    [InlineData("/iiif-img/v2/2/2/test-image/full/max/0/default.jpg", "/v2/2/2/test-image/full/400,400/0/default.jpg", ProxyDestination.Thumbs)] // /full/max - v2
    [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg", "/2/2/test-image/full/400,400/0/default.jpg", ProxyDestination.Thumbs)] // /full/max - v3
    [InlineData("/iiif-img/2/2/test-image/full/^max/0/default.jpg", "/2/2/test-image/full/400,400/0/default.jpg", ProxyDestination.Thumbs)] // /full/^max - v3
    [InlineData("/iiif-img/2/2/test-image/square/!100,150/0/default.jpg", "/2/2/test-image/square/100,100/0/default.jpg", ProxyDestination.ResizeThumbs)] // square
    public async Task HandleRequest_ProxiesToThumbs_ForFullThatAreSmallEnough_RewritingIfRequired(string path, string expectedProxyPath, ProxyDestination destination)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");

        var settings = CreateOrchestratorSettings();
        settings.Proxy.CanResizeThumbs = true;
        var sut = GetImageRequestHandlerWithMockPathParser(orchestratorSettings: settings);

        // Image is 2000x2000 but only 400 maxWidth
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[400, 400], [150, 150]], S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 400, Size = Size.Square(2000),
            });
        
        var expected = $"thumbs{expectedProxyPath}";

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(destination);
        result.Path.Should().Be(expected);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/90,/0/default.jpg", false, "0,0,512,512/90,/0/default.jpg", false)] // UV without ?t=
    [InlineData("/iiif-img/v2/2/2/test-image/0,0,512,512/full/0/default.jpg", true, "0,0,512,512/512,512/0/default.jpg", true)] // /full
    [InlineData("/iiif-img/v2/2/2/test-image/0,0,512,512/max/0/default.jpg", true, "0,0,512,512/5000,5000/0/default.jpg", true)] // v2 /max
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/max/0/default.jpg", true, "0,0,512,512/512,512/0/default.jpg", false)] // v3 /max
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/^max/0/default.jpg", true, "0,0,512,512/^5000,5000/0/default.jpg", false)] // v3 /^max
    [InlineData("/iiif-img/v2/2/2/test-image/pct:0,0,512,512/full/0/default.jpg", true, "pct:0,0,512,512/1000,1000/0/default.jpg", true)] // pct: full v2
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/default.png", false, "0,0,512,512/100,100/0/default.png", false)] // png
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/default.tif", false, "0,0,512,512/100,100/0/default.tif", false)] // tif
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/90/default.jpg", false, "0,0,512,512/100,100/90/default.jpg", false)] // rotation
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/!0/default.jpg", false, "0,0,512,512/100,100/!0/default.jpg", false)] // rotation / mirrored
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/bitonal.jpg", false, "0,0,512,512/100,100/0/bitonal.jpg", false)] // bitonal
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/gray.jpg", false, "0,0,512,512/100,100/0/gray.jpg", false)] // gray
    [InlineData("/iiif-img/2/2/test-image/0,0,100,100/!512,512/0/gray.png", false, "0,0,100,100/100,100/0/gray.png", false)] // largest possible under maxWidth
    [InlineData("/iiif-img/2/2/test-image/0,0,100,100/^!5120,5120/0/gray.png", false, "0,0,100,100/^5000,5000/0/gray.png", false)] // largest possible under maxWidth
    public async Task HandleRequest_ProxiesToImageServer_ForAllTileRequests(string path, bool knownThumb, string expectedProxyPath, bool version2)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        List<int[]> openSizes = knownThumb ? [[150, 150]] : [];

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1000),
            });
        
        var destination = version2 ? "cantaloupe-2" : "cantaloupe-3";
        var expected = $"{destination}/path/{expectedProxyPath}";

        // Act
        var result = (ProxyImageServerResult)await sut.HandleRequest(context);
            
        // Assert
        result.Target.Should().Be(ProxyDestination.ImageServer);
        result.Path.Should().Be(expected);
    }

    [Theory]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v2/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v3/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-3", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.IIPImage, "/iiif-img/v2/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v2/2/2/test-image/5,5,5,5/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.ImageServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v3/2/2/test-image/5,5,5,5/^90,/0/default.jpg", "cantaloupe-3", ProxyDestination.ImageServer)]
    [InlineData(ImageServer.IIPImage, "/iiif-img/v2/2/2/test-image/5,5,5,5/90,/0/default.jpg", "iip", ProxyDestination.ImageServer)]
    public async Task HandleRequest_ProxiesToCorrectImageServerEndpoint_ForVersionedRequests(ImageServer imageServer,
        string path, string startsWith, ProxyDestination proxyDestination)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");

        var settings = CreateOrchestratorSettings();
        settings.ImageServer = imageServer;
        var sut = GetImageRequestHandlerWithMockPathParser(orchestratorSettings: settings);
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [], S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1000),
            });

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);

        // Assert
        result.Target.Should().Be(proxyDestination);
        result.HasPath.Should().BeTrue();
        result.Path.Should().StartWith(startsWith);
    }

    [Theory]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v1/2/2/test-image/0,0,512,512/90,/0/default.jpg")] // Unknown version
    [InlineData(ImageServer.IIPImage, "/iiif-img/v3/2/2/test-image/0,0,512,512/90,/0/default.jpg")] // Unsupported version
    public async Task HandleRequest_Returns400_IfMatchingImageServerNotFound_TileRequest(ImageServer imageServer, string path)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");

        var settings = CreateOrchestratorSettings();
        settings.ImageServer = imageServer;
        var sut = GetImageRequestHandlerWithMockPathParser(orchestratorSettings: settings);
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [], S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1512),
            });

        // Act
        var result = (StatusCodeResult)await sut.HandleRequest(context);
            
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Fact]
    public async Task HandleRequest_Returns400_IfMatchingImageServerNotFound_Full()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/v10/2/2/test-image/full/90,/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");

        var settings = CreateOrchestratorSettings();
        var sut = GetImageRequestHandlerWithMockPathParser(orchestratorSettings: settings);
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [], S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1512),
            });

        // Act
        var result = (StatusCodeResult)await sut.HandleRequest(context);
            
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/150,150/0/default.jpg", ProxyDestination.Thumbs)]
    [InlineData("/iiif-img/2/2/test-image/5,5,90,90/90,/0/default.jpg", ProxyDestination.ImageServer)]
    [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg", ProxyDestination.SpecialServer)] 
    public async Task HandleRequest_ProxiesAll_WithCustomHeaders(string path, ProxyDestination destination)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => customHeaderRepository.GetForCustomer(2)).Returns(new List<CustomHeader>
        {
            new() { Space = 2, Role = null, Key = "x-test-header", Value = "test" },
            new() { Space = null, Role = null, Key = "x-test-header-2", Value = "test" },
        });
            
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        List<int[]> openSizes = [[150, 150]];

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, Size = new Size(1000, 1512),
            });

        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Headers.Should().ContainKeys("x-test-header", "x-test-header-2");
        result.Target.Should().Be(destination);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg")] // special
    [InlineData("/iiif-img/2/2/test-image/full/!150,150/0/default.jpg")] // thumbs
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/default.png")] // tile (image-server)
    public async Task HandleRequest_Returns404_IfNoReingestAndS3LocationEmpty_RegardlessOfDestination(string path)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = [[150, 150]], S3Location = "", MaxWidth = 5000,
                Channels = AvailableDeliveryChannel.Image, Reingest = false, Size = new Size(1512, 1000)
            });

        // Act
        var result = (StatusCodeResult)await sut.HandleRequest(context);
            
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    # region Strict/Lax mode handling
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/full/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/full/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/square/full/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/pct:0,0,50,50/full/0/default.jpg")]
    public async Task HandleRequest_StrictMode_RejectsV3FullSize(string path)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000,
                Size = new Size(1000, 1000), S3Location = "s3://storage/2/2/test-image",
            });
        
        // Act
        var result = (StatusCodeResult)await sut.HandleRequest(context);
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/full/0/default.jpg", "full/1000,1000/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/full/0/default.jpg", "0,0,512,512/512,512/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/square/full/0/default.jpg", "square/1000,1000/0/default.jpg")]
    [InlineData("/iiif-img/2/2/test-image/pct:0,0,50,50/full/0/default.jpg", "pct:0,0,50,50/500,500/0/default.jpg")]
    public async Task HandleRequest_LaxMode_TreatsV3FullSizeAsMax(string path, string expectedProxyPath)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var settings = CreateOrchestratorSettings();
        settings.StrictImageRequestParsing = false;
        var sut = GetImageRequestHandlerWithMockPathParser(orchestratorSettings: settings);

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Channels = AvailableDeliveryChannel.Image, MaxWidth = 5000, 
                Size = new Size(1000, 1000), S3Location = "s3://storage/2/2/test-image",
            });
        
        // Act
        var result = (ProxyActionResult)await sut.HandleRequest(context);
            
        // Assert
        result.Path.Should().Contain(expectedProxyPath);
    }
    # endregion

    private ImageRequestHandler GetImageRequestHandlerWithMockPathParser(bool mockPathParser = false,
        OrchestratorSettings orchestratorSettings = null)
    {
        // mockPathParser = true will return A.Fake, else return actual impl with fake repo 
        var requestProcessor = new AssetRequestProcessor(new NullLogger<AssetRequestProcessor>(), assetTracker,
            mockPathParser ? assetDeliveryPathParser : assetDeliveryPathParserImpl);
        return new(new NullLogger<ImageRequestHandler>(), requestProcessor, scopeFactory, customHeaderRepository,
            Options.Create(orchestratorSettings ?? CreateOrchestratorSettings()));
    }
}
