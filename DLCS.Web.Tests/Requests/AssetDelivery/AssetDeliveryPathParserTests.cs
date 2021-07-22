using System;
using System.Threading.Tasks;
using DLCS.Model.PathElements;
using DLCS.Web.Requests.AssetDelivery;
using FakeItEasy;
using FluentAssertions;
using Xunit;

namespace DLCS.Web.Tests.Requests.AssetDelivery
{
    public class AssetDeliveryPathParserTests
    {
        private readonly IPathCustomerRepository pathCustomerRepository;
        private readonly AssetDeliveryPathParser sut;

        public AssetDeliveryPathParserTests()
        {
            pathCustomerRepository = A.Fake<IPathCustomerRepository>();
            sut = new AssetDeliveryPathParser(pathCustomerRepository);
        }

        [Fact]
        public async Task Parse_WithCustomerId_ReturnsCorrectRequest()
        {
            // Arrange
            const string path = "/thumbs/99/1/the-astronaut";
            var customer = new CustomerPathElement(99, "Test-Customer");
            A.CallTo(() => pathCustomerRepository.GetCustomer("99"))
                .Returns(customer);

            // Act
            var thumbnailRequest = await sut.Parse(path);
            
            // Assert
            thumbnailRequest.RoutePrefix.Should().Be("thumbs");
            thumbnailRequest.CustomerPathValue.Should().Be("99");
            thumbnailRequest.Customer.Should().Be(customer);
            thumbnailRequest.BasePath.Should().Be("/thumbs/99/1/");
            thumbnailRequest.Space.Should().Be(1);
            thumbnailRequest.AssetPath.Should().Be("the-astronaut");
            thumbnailRequest.AssetId.Should().Be("the-astronaut");
        }

        [Fact]
        public async Task Parse_WithCustomerName_ReturnsCorrectRequest()
        {
            // Arrange
            const string path = "/thumbs/test-customer/1/the-astronaut";
            var customer = new CustomerPathElement(99, "test-customer");
            A.CallTo(() => pathCustomerRepository.GetCustomer("test-customer"))
                .Returns(customer);

            // Act
            var thumbnailRequest = await sut.Parse(path);

            // Assert
            thumbnailRequest.RoutePrefix.Should().Be("thumbs");
            thumbnailRequest.CustomerPathValue.Should().Be("test-customer");
            thumbnailRequest.Customer.Should().Be(customer);
            thumbnailRequest.BasePath.Should().Be("/thumbs/test-customer/1/");
            thumbnailRequest.Space.Should().Be(1);
            thumbnailRequest.AssetPath.Should().Be("the-astronaut");
            thumbnailRequest.AssetId.Should().Be("the-astronaut");
        }
        
        [Fact]
        public async Task Parse_WithCustomerName_FullParse()
        {
            // Arrange
            const string path = "/iiif-img/test-customer/1/the-astronaut/full/!800,400/0/default.jpg";
            var customer = new CustomerPathElement(99, "test-customer");
            A.CallTo(() => pathCustomerRepository.GetCustomer("test-customer"))
                .Returns(customer);

            // Act
            var imageRequest = await sut.Parse(path);

            // Assert
            imageRequest.RoutePrefix.Should().Be("iiif-img");
            imageRequest.CustomerPathValue.Should().Be("test-customer");
            imageRequest.Customer.Should().Be(customer);
            imageRequest.BasePath.Should().Be("/iiif-img/test-customer/1/");
            imageRequest.Space.Should().Be(1);
            imageRequest.AssetPath.Should().Be("the-astronaut/full/!800,400/0/default.jpg");
            imageRequest.AssetId.Should().Be("the-astronaut");
            imageRequest.IIIFImageRequest.ImageRequestPath.Should().Be("/full/!800,400/0/default.jpg");
        }

        [Fact]
        public async Task Parse_ThrowsFormatException_IfPathInUnknownFormat()
        {
            // Arrange
            const string path = "/iiif-img/full/!800,400/0/default.jpg";
            
            // Act
            Func<Task> action = () => sut.Parse(path);
            
            // Assert
            await action.Should().ThrowAsync<FormatException>();
        }
    }
}