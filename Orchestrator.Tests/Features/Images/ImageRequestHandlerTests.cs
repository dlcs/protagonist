﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DLCS.Core.Types;
using DLCS.Model.Assets.CustomHeaders;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
using FluentAssertions;
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
using Xunit;

namespace Orchestrator.Tests.Features.Images
{
    public class ImageRequestHandlerTests
    {
        private readonly IAssetTracker assetTracker;
        private readonly IAssetDeliveryPathParser assetDeliveryPathParser;
        private readonly IPathCustomerRepository customerRepository;
        private readonly AssetDeliveryPathParser assetDeliveryPathParserImpl;
        private readonly IAssetAccessValidator accessValidator;
        private readonly IOptions<OrchestratorSettings> defaultSettings;
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
            defaultSettings = Options.Create(new OrchestratorSettings());

            scopeFactory = A.Fake<IServiceScopeFactory>();
            var scope = A.Fake<IServiceScope>();
            A.CallTo(() => scopeFactory.CreateScope()).Returns(scope);
            A.CallTo(() => scope.ServiceProvider.GetService(typeof(IAssetAccessValidator))).Returns(accessValidator);
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

        [Fact]
        public async Task Handle_Request_Returns401_IfAssetRequiresAuth_AndUserCannotAccess()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/!200,200/0/default.jpg";

            var roles = new List<string> { "role" };
            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetTracker.GetOrchestrationAsset(new AssetId(2, 2, "test-image")))
                .Returns(new OrchestrationImage { Roles = roles });
            A.CallTo(() => accessValidator.TryValidate(2, roles, AuthMechanism.Cookie))
                .Returns(AssetAccessResult.Unauthorized);
            var sut = GetImageRequestHandlerWithMockPathParser();

            // Act
            var result = await sut.HandleRequest(context);
            
            // Assert
            result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData(AssetAccessResult.Open)]
        [InlineData(AssetAccessResult.Authorized)]
        public async Task Handle_Request_ProxiesToImageServer_IfAssetRequiresAuth_AndUserAuthorised(
            AssetAccessResult accessResult)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/,900/0/default.jpg";

            var roles = new List<string> { "role" };
            var assetId = new AssetId(2, 2, "test-image");
            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => assetTracker.GetOrchestrationAsset(assetId))
                .Returns(new OrchestrationImage
                    { AssetId = assetId, Roles = roles, OpenThumbs = new List<int[]> { new[] { 150, 150 } } });
            A.CallTo(() => accessValidator.TryValidate(2, roles, AuthMechanism.Cookie)).Returns(accessResult);
            var sut = GetImageRequestHandlerWithMockPathParser(
                settings: Options.Create(new OrchestratorSettings
                {
                    ImageFolderTemplateImageServer = "/path",
                    Proxy = new()
                }));

            // Act
            var result = await sut.HandleRequest(context) as ProxyImageServerResult;

            // Assert
            result.Target.Should().Be(ProxyDestination.ImageServer);
            result.HasPath.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_Request_ProxiesToThumbs_IfFullOrMaxRegion_AndKnownSize()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/!100,150/0/default.jpg";

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            var assetId = new AssetId(2, 2, "test-image");
            A.CallTo(() => assetTracker.GetOrchestrationAsset(assetId))
                .Returns(new OrchestrationImage
                    { AssetId = assetId, OpenThumbs = new List<int[]> { new[] { 150, 150 } } });
            var sut = GetImageRequestHandlerWithMockPathParser(
                settings: Options.Create(new OrchestratorSettings
                {
                    ImageFolderTemplateImageServer = "/path",
                    Proxy = new ProxySettings()
                }));

            // Act
            var result = await sut.HandleRequest(context) as ProxyActionResult;
            
            // Assert
            result.Target.Should().Be(ProxyDestination.Thumbs);
            result.Path.Should().Be("thumbs/2/2/test-image/full/!100,150/0/default.jpg");
        }
        
        [Theory]
        [InlineData("/iiif-img/2/2/test-image/full/90,/0/default.jpg", false)] // UV without ?t=
        [InlineData("/iiif-img/2/2/test-image/full/full/0/default.jpg", true)] // /full/full
        [InlineData("/iiif-img/2/2/test-image/full/max/0/default.jpg", true)] // /full/max
        public async Task Handle_Request_ProxiesToImageServer_ForAllOtherCases(string path, bool knownThumb)
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = path;

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            var assetId = new AssetId(2, 2, "test-image");
            
            var sut = GetImageRequestHandlerWithMockPathParser(
                settings: Options.Create(new OrchestratorSettings
                {
                    ImageFolderTemplateImageServer = "/path",
                    Proxy = new ProxySettings()
                }));

            List<int[]> openSizes = knownThumb
                ? new List<int[]> { new[] { 150, 150 } }
                : new List<int[]>();

            A.CallTo(() => assetTracker.GetOrchestrationAsset(assetId))
                .Returns(new OrchestrationImage { AssetId = assetId, OpenThumbs = openSizes });

            // Act
            var result = await sut.HandleRequest(context) as ProxyImageServerResult;
            
            // Assert
            result.Target.Should().Be(ProxyDestination.ImageServer);
            result.HasPath.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_Request_ProxiesToImageServer_WithCustomHeaders()
        {
            // Arrange
            var context = new DefaultHttpContext();
            context.Request.Path = "/iiif-img/2/2/test-image/full/max/0/default.jpg";

            A.CallTo(() => customerRepository.GetCustomer("2")).Returns(new CustomerPathElement(2, "Test-Cust"));
            A.CallTo(() => customHeaderRepository.GetForCustomer(2)).Returns(new List<CustomHeader>
            {
                new() { Space = 2, Role = null, Key = "x-test-header", Value = "test" },
                new() { Space = null, Role = null, Key = "x-test-header-2", Value = "test" },
            });
            
            var assetId = new AssetId(2, 2, "test-image");
            
            var sut = GetImageRequestHandlerWithMockPathParser(
                settings: Options.Create(new OrchestratorSettings
                {
                    ImageFolderTemplateImageServer = "/path",
                    Proxy = new ProxySettings()
                }));

            List<int[]> openSizes = new List<int[]> { new[] { 150, 150 } };

            A.CallTo(() => assetTracker.GetOrchestrationAsset(assetId))
                .Returns(new OrchestrationImage { AssetId = assetId, OpenThumbs = openSizes });

            // Act
            var result = await sut.HandleRequest(context) as ProxyImageServerResult;
            
            // Assert
            result.Headers.Should().ContainKeys("x-test-header", "x-test-header-2");
        }

        private ImageRequestHandler GetImageRequestHandlerWithMockPathParser(bool mockPathParser = false,
            IOptions<OrchestratorSettings> settings = null)
        {
            var requestProcessor = new AssetRequestProcessor(new NullLogger<AssetRequestProcessor>(), assetTracker,
                mockPathParser ? assetDeliveryPathParser : assetDeliveryPathParserImpl);
            return new(new NullLogger<ImageRequestHandler>(), requestProcessor, scopeFactory, customHeaderRepository,
                settings ?? defaultSettings);
        }
    }
}