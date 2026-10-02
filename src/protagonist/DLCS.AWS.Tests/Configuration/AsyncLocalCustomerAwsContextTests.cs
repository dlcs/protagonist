using DLCS.AWS.Configuration;

namespace DLCS.AWS.Tests.Configuration;

public class AsyncLocalCustomerAwsContextTests
{
    private readonly ICustomerAwsContext sut = new AsyncLocalCustomerAwsContext();

    [Fact]
    public void CurrentCustomer_Null_IfNotSet()
    {
        sut.CurrentCustomer.Should().BeNull();
    }

    [Fact]
    public void SetCustomer_SetsCurrentCustomer_UntilDisposed()
    {
        // Act
        using (sut.SetCustomer(99))
        {
            // Assert
            sut.CurrentCustomer.Should().Be(99);
        }

        sut.CurrentCustomer.Should().BeNull();
    }

    [Fact]
    public void SetCustomer_RestoresPreviousCustomer_WhenNested()
    {
        using (sut.SetCustomer(10))
        {
            using (sut.SetCustomer(20))
            {
                sut.CurrentCustomer.Should().Be(20);
            }

            sut.CurrentCustomer.Should().Be(10, "the previous customer is restored");
        }

        sut.CurrentCustomer.Should().BeNull();
    }

    [Fact]
    public async Task SetCustomer_FlowsThroughAsyncOperations()
    {
        // Arrange
        int? customerInTask = null;

        // Act
        using (sut.SetCustomer(99))
        {
            await Task.Run(async () =>
            {
                await Task.Yield();
                customerInTask = sut.CurrentCustomer;
            });
        }

        // Assert
        customerInTask.Should().Be(99);
    }

    [Fact]
    public async Task SetCustomer_DoesNotLeakBetweenConcurrentOperations()
    {
        // Arrange
        async Task<int?> GetCustomerAfterSetting(int customer)
        {
            using (sut.SetCustomer(customer))
            {
                await Task.Delay(10);
                return sut.CurrentCustomer;
            }
        }

        // Act
        var results = await Task.WhenAll(GetCustomerAfterSetting(1), GetCustomerAfterSetting(2),
            GetCustomerAfterSetting(3));

        // Assert
        results.Should().BeEquivalentTo(new int?[] { 1, 2, 3 });
        sut.CurrentCustomer.Should().BeNull();
    }
}
