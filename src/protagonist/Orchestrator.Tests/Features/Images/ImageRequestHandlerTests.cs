using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using DLCS.Core.Types;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
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

    public ImageRequestHandlerTests()
    {
        assetTracker = A.Fake<IAssetTracker>();
        assetDeliveryPathParser = A.Fake<IAssetDeliveryPathParser>();
        customerRepository = A.Fake<IPathCustomerRepository>();
        accessValidator = A.Fake<IAssetAccessValidator>();
        assetDeliveryPathParserImpl = new AssetDeliveryPathParser(customerRepository);
        customHeaderRepository = A.Fake<ICustomHeaderRepository>();

        scopeFactory = A.Fake<IServiceScopeFactory>();
        var scope = A.Fake<IServiceScope>();
        A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
        A.CallTo(() => scope.ServiceProvider.GetService(typeof(IAssetAccessValidator))).Returns(accessValidator);
    }

    private static OrchestratorSettings CreateOrchestratorSettings()
    {
        return new OrchestratorSettings
        {
            Proxy = new(),
            ImageServerPathConfig = new()
            {
                [ImageServer.Cantaloupe] = new ImageServerConfig
                {
                    Separator = "%2F",
                    PathTemplate = "/path",
                    VersionPathTemplates = new Dictionary<Version, string>
                    {
                        [Version.V3] = "cantaloupe-3",
                        [Version.V2] = "cantaloupe-2"
                    }
                },
                [ImageServer.IIPImage] = new ImageServerConfig
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
    }

    [Fact]
    public async Task HandleRequest_Returns404_IfAssetPathParserThrowsKeyNotFound()
    {
        // Arrange
        A.CallTo(() => assetDeliveryPathParser.Parse<ImageAssetDeliveryRequest>(A<string>._))
            .ThrowsAsync(new KeyNotFoundException());
        var sut = GetImageRequestHandlerWithMockPathParser(true);
            
        // Act
        var result = await sut.HandleRequest(new DefaultHttpContext());
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
        
    [Fact]
    public async Task HandleRequest_Returns400_IfAssetPathParserThrowsFormatException()
    {
        // NOTE - routes should prevent this from ever happening
            
        // Arrange
        A.CallTo(() => assetDeliveryPathParser.Parse<ImageAssetDeliveryRequest>(A<string>._))
            .ThrowsAsync(new FormatException());
        var sut = GetImageRequestHandlerWithMockPathParser(true);
            
        // Act
        var result = await sut.HandleRequest(new DefaultHttpContext());
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
        
    [Fact]
    public async Task HandleRequest_Returns400_IfAssetPathParserThrowsException()
    {
        // NOTE - routes should prevent this from ever happening
            
        // Arrange
        A.CallTo(() => assetDeliveryPathParser.Parse<ImageAssetDeliveryRequest>(A<string>._))
            .ThrowsAsync(new ApplicationException());
        var sut = GetImageRequestHandlerWithMockPathParser(true);
            
        // Act
        var result = await sut.HandleRequest(new DefaultHttpContext());
            
        // Assert
        result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
                Roles = roles, RequiresAuth = true, Channels = AvailableDeliveryChannel.Image, S3Location = "s3://"
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
    public async Task HandleRequest_ProxiesToSpecialServer_IfAssetRequiresAuth_AndUserNotAuthorised_ButFullRequestSmallerThanMaxUnauthorised(
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
                AssetId = assetId, Roles = roles, OpenThumbs = new List<int[]> { new[] { 150, 150 } },
                MaxUnauthorised = 900, Width = 1800, Height = 1800, RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;

        // Assert
        result.Target.Should().Be(ProxyDestination.SpecialServer);
        result.HasPath.Should().BeTrue();
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).MustNotHaveHappened();
    }
    
    [Fact]
    public async Task HandleRequest_ProxiesToSpecialServer_IfAssetRequiresAuth_AndUserNotAuthorised_ButFullRequestSmallerThanMaxUnauthorised_MaxSize()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/max/0/default.jpg";

        var roles = new List<string> { "role" };
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, Roles = roles, OpenThumbs = new List<int[]> { new[] { 150, 150 } },
                MaxUnauthorised = 900, Width = 900, Height = 900, RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;

        // Assert
        result.Target.Should().Be(ProxyDestination.SpecialServer);
        result.HasPath.Should().BeTrue();
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("/full/901,901/", "Size too large")]
    [InlineData("/full/max/", "Max size")]
    [InlineData("/0,0,512,512/900,/", "Tiled region")]
    [InlineData("/pct:0,0,512,512/!10,10/", "Percent region")]
    [InlineData("/square/!90,90/", "Square region")]
    public async Task HandleRequest_Returns401_IfAssetRequiresAuth_AndUserNotAuthorised_AndRequestNotForMaxUnauthorised(
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
                Roles = roles, MaxUnauthorised = 900, Width = 1800, Height = 1800, RequiresAuth = true,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
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
    public async Task HandleRequest_ProxiesToThumbs_IfRequiresAuth_AndFullRegionOfKnownSize_SmallerThanMaxUnauthorised()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/iiif-img/2/2/test-image/full/!150,150/0/default.jpg";

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = new List<int[]> { new[] { 150, 150 } }, Height = 1000, Width = 1000,
                RequiresAuth = true, Roles = new List<string> { "role" }, MaxUnauthorised = 200,
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/full/!150,150/0/default.jpg");
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
                AssetId = assetId, OpenThumbs = new List<int[]> { new[] { 150, 150 } },
                S3Location = "s3://storage/2/2/test-image", Channels = AvailableDeliveryChannel.Image
            });
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;
            
        // Assert
        result.Target.Should().Be(ProxyDestination.Thumbs);
        result.Path.Should().Be("thumbs/2/2/test-image/full/!150,150/0/default.jpg");
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
                AssetId = assetId, Roles = roles, OpenThumbs = new List<int[]> { new[] { 150, 150 } },
                RequiresAuth = true, Height = 1000, Width = 1000, MaxUnauthorised = 300,
                Channels = AvailableDeliveryChannel.Image, Reingest = true
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(accessResult);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as ProxyImageServerResult;

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
                AssetId = assetId, Roles = roles, OpenThumbs = new List<int[]> { new[] { 150, 150 } },
                RequiresAuth = true, Height = 1000, Width = 1000, MaxUnauthorised = 300,
                Channels = AvailableDeliveryChannel.Image, Reingest = false
            });
        A.CallTo(() => accessValidator.TryValidate(A<AssetId>.That.Matches(a => a.Customer == 2), roles,
            AuthMechanism.Cookie, CancellationToken.None)).Returns(accessResult);
        var sut = GetImageRequestHandlerWithMockPathParser();

        // Act
        var result = await sut.HandleRequest(context) as StatusCodeResult;

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg")] // full/<size>
    [InlineData("/iiif-img/2/2/test-image/full/full/0/default.jpg")] // /full/full
    [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg")] // /full/max
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/default.png")] // png
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/default.tif")] // tif
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/90/default.jpg")] // rotation
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/!0/default.jpg")] // rotation / mirrored
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/bitonal.jpg")] // bitonal
    [InlineData("/iiif-img/2/2/test-image/full/!100,150/0/gray.jpg")] // gray
    public async Task HandleRequest_ProxiesToSpecialServer_ForAllFull(string path)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        List<int[]> openSizes = new List<int[]> { new[] { 150, 150 } };

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });

        var expected = $"cantaloupe-3s3:%2F%2Fstorage%2F2%2F2%2Ftest-image/{string.Join("/", path.Split("/")[5..])}";

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;
            
        // Assert
        result.Target.Should().Be(ProxyDestination.SpecialServer);
        result.Path.Should().Be(expected);
    }
        
    [Theory]
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/90,/0/default.jpg", false)] // UV without ?t=
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/full/0/default.jpg", true)] // /full/full
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/max/0/default.jpg", true)] // /full/max
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/default.png", false)] // png
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/default.tif", false)] // tif
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/90/default.jpg", false)] // rotation
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/!0/default.jpg", false)] // rotation / mirrored
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/bitonal.jpg", false)] // bitonal
    [InlineData("/iiif-img/2/2/test-image/0,0,512,512/!100,150/0/gray.jpg", false)] // gray
    public async Task HandleRequest_ProxiesToImageServer_ForAllTileRequests(string path, bool knownThumb)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        A.CallTo(() => customerRepository.GetCustomerPathElement("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
        var assetId = new AssetId(2, 2, "test-image");
            
        var sut = GetImageRequestHandlerWithMockPathParser();

        List<int[]> openSizes = knownThumb
            ? new List<int[]> { new[] { 150, 150 } }
            : new List<int[]>();

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });
        
        var expected = $"cantaloupe-3/path/{string.Join("/", path.Split("/")[5..])}";

        // Act
        var result = await sut.HandleRequest(context) as ProxyImageServerResult;
            
        // Assert
        result.Target.Should().Be(ProxyDestination.ImageServer);
        result.Path.Should().Be(expected);
    }

    [Theory]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v2/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v3/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-3", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.IIPImage, "/iiif-img/v2/2/2/test-image/full/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.SpecialServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v2/2/2/test-image/5,5,5,5/90,/0/default.jpg", "cantaloupe-2", ProxyDestination.ImageServer)]
    [InlineData(ImageServer.Cantaloupe, "/iiif-img/v3/2/2/test-image/5,5,5,5/90,/0/default.jpg", "cantaloupe-3", ProxyDestination.ImageServer)]
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
                AssetId = assetId, OpenThumbs = new List<int[]>(), S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;

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
                AssetId = assetId, OpenThumbs = new List<int[]>(), S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });

        // Act
        var result = await sut.HandleRequest(context) as StatusCodeResult;
            
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
                AssetId = assetId, OpenThumbs = new List<int[]>(), S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });

        // Act
        var result = await sut.HandleRequest(context) as StatusCodeResult;
            
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/iiif-img/2/2/test-image/full/!150,150/0/default.jpg", ProxyDestination.Thumbs)]
    [InlineData("/iiif-img/2/2/test-image/5,5,5,5/90,/0/default.jpg", ProxyDestination.ImageServer)]
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

        List<int[]> openSizes = new List<int[]> { new[] { 150, 150 } };

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "s3://storage/2/2/test-image",
                Channels = AvailableDeliveryChannel.Image
            });

        // Act
        var result = await sut.HandleRequest(context) as ProxyActionResult;
            
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

        List<int[]> openSizes = new List<int[]> { new[] { 150, 150 } };

        A.CallTo(() => assetTracker.GetOrchestrationAsset<OrchestrationImage>(assetId))
            .Returns(new OrchestrationImage
            {
                AssetId = assetId, OpenThumbs = openSizes, S3Location = "",
                Channels = AvailableDeliveryChannel.Image, Reingest = false
            });

        // Act
        var result = await sut.HandleRequest(context) as StatusCodeResult;
            
        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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