using System;
using System.Threading;
using DLCS.Model.Policies;
using DLCS.Model.Storage;
using DLCS.Repository.Storage;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Storage;

[Trait("Category", "Integration")]
[Collection(DatabaseCollection.CollectionName)]
public class CustomerStorageRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly IPolicyRepository policyRepository;
    private readonly CustomerStorageRepository sut;

    public CustomerStorageRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();

        policyRepository = A.Fake<IPolicyRepository>();
        A.CallTo(() => policyRepository.GetStoragePolicy("default", A<CancellationToken>._))
            .Returns(new StoragePolicy
            {
                Id = "default",
                MaximumNumberOfStoredImages = 1000000,
                MaximumTotalSizeOfStoredImages = 1000000000
            });

        sut = new CustomerStorageRepository(dbContext, policyRepository, NullLogger<CustomerStorageRepository>.Instance);
    }

    [Fact]
    public async Task GetStorageMetrics_ReturnsNullSpaceRow()
    {
        // Arrange
        const int customerId = 10;
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, numberOfImages: 50,
            sizeOfStored: 500L);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetStorageMetrics(customerId, CancellationToken.None);

        // Assert
        result.CurrentNumberOfStoredImages.Should().Be(50);
        result.CurrentTotalSizeStoredImages.Should().Be(500L);
        result.Policy.Id.Should().Be("default");
    }

    [Fact]
    public async Task GetStorageMetrics_Throws_WhenNoAggregateRowExists()
    {
        // Arrange - no CustomerStorage row seeded for this customer
        const int customerId = 11;

        // Act & Assert - absence of the aggregate row is a bug (CreateCustomer + migration guarantee it)
        await sut.Invoking(s => s.GetStorageMetrics(customerId, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetStorageMetrics_UsesDefaultPolicy_WhenStoragePolicyIsEmpty()
    {
        // Arrange
        const int customerId = 12;
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, storagePolicy: "");
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetStorageMetrics(customerId, CancellationToken.None);

        // Assert
        A.CallTo(() => policyRepository.GetStoragePolicy("default", A<CancellationToken>._))
            .MustHaveHappened();
        result.Policy.Id.Should().Be("default");
    }

    [Fact]
    public async Task GetStorageMetrics_IgnoresRealSpaceRows()
    {
        // Arrange
        const int customerId = 13;
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, numberOfImages: 5,
            sizeOfStored: 100L);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, space: 1, numberOfImages: 200,
            sizeOfStored: 9000L);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetStorageMetrics(customerId, CancellationToken.None);

        // Assert - returns only the null-space aggregate, not the sum with space 1
        result.CurrentNumberOfStoredImages.Should().Be(5);
        result.CurrentTotalSizeStoredImages.Should().Be(100L);
    }

    [Fact]
    public async Task GetCustomerStorageSummary_ReturnsNullSpaceRowValues()
    {
        // Arrange
        const int customerId = 14;
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, numberOfImages: 77,
            sizeOfStored: 7777L, sizeOfThumbs: 111L);
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, space: 1, numberOfImages: 10,
            sizeOfStored: 500L);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetCustomerStorageSummary(customerId, CancellationToken.None);

        // Assert - returns the null-space aggregate row, not the sum of per-space rows
        result.NumberOfStoredImages.Should().Be(77);
        result.TotalSizeOfStoredImages.Should().Be(7777L);
        result.TotalSizeOfThumbnails.Should().Be(111L);
    }

    [Fact]
    public async Task GetCustomerStorageSummary_ReturnsEmpty_WhenNoNullSpaceRow()
    {
        // Arrange
        const int customerId = 15;
        await dbContext.CustomerStorages.AddTestCustomerStorage(customer: customerId, space: 1, numberOfImages: 50,
            sizeOfStored: 500L);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await sut.GetCustomerStorageSummary(customerId, CancellationToken.None);

        // Assert - no aggregate row means zeroed summary, not a sum of real-space rows
        result.NumberOfStoredImages.Should().Be(0);
        result.TotalSizeOfStoredImages.Should().Be(0);
    }
}
