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
            thumbnailRequest.Customer.Should().Be(customer);
            thumbnailRequest.BasePath.Should().Be("/thumbs/99/1/");
            thumbnailRequest.Space.Should().Be(1);
            thumbnailRequest.AssetPath.Should().Be("the-astronaut");
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
            thumbnailRequest.Customer.Should().Be(customer);
            thumbnailRequest.BasePath.Should().Be("/thumbs/test-customer/1/");
            thumbnailRequest.Space.Should().Be(1);
            thumbnailRequest.AssetPath.Should().Be("the-astronaut");
        }
    }
}