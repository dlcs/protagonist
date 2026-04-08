using System;
using API.Features.AdjunctQueues.Converters;
using DLCS.Model.Assets;

namespace API.Tests.Features.AdjunctQueues.Converters;

public class AdjunctBatchConverterTests
{
    private const int CustomerId = 99;
    private const int BatchId = 1234;
    private const string BaseUrl = "https://dlcs.example";

    [Fact]
    public void ToHydra_ConvertsAllFields()
    {
        // Arrange
        var submitted = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var batch = new AdjunctBatch
        {
            Id = BatchId,
            Customer = CustomerId,
            Submitted = submitted,
            Count = 5,
            Completed = 3,
            Errors = 1,
            Finished = null,
        };

        // Act
        var hydra = batch.ToHydra(BaseUrl);

        // Assert
        hydra.ModelId.Should().Be(BatchId);
        hydra.CustomerId.Should().Be(CustomerId);
        hydra.Submitted.Should().Be(submitted);
        hydra.Count.Should().Be(5);
        hydra.Completed.Should().Be(3);
        hydra.Errors.Should().Be(1);
        hydra.Finished.Should().BeNull();
    }

    [Fact]
    public void ToHydra_SetsFinished_WhenPresent()
    {
        // Arrange
        var finished = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var batch = new AdjunctBatch
        {
            Id = BatchId,
            Customer = CustomerId,
            Submitted = DateTime.UtcNow,
            Finished = finished,
        };

        // Act
        var hydra = batch.ToHydra(BaseUrl);

        // Assert
        hydra.Finished.Should().Be(finished);
    }

    [Fact]
    public void ToHydra_IdMatchesExpectedFormat()
    {
        // Arrange
        var batch = new AdjunctBatch
        {
            Id = BatchId,
            Customer = CustomerId,
            Submitted = DateTime.UtcNow,
        };

        // Act
        var hydra = batch.ToHydra(BaseUrl);

        // Assert
        hydra.Id.Should().Be($"{BaseUrl}/customers/{CustomerId}/adjunctQueue/batches/{BatchId}");
    }

    [Fact]
    public void ToDlcsModel_ConvertsAllFields()
    {
        // Arrange
        var submitted = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var finished = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var hydra = new DLCS.HydraModel.AdjunctBatch(BaseUrl, CustomerId, BatchId)
        {
            Submitted = submitted,
            Count = 10,
            Completed = 8,
            Errors = 2,
            Finished = finished,
        };

        // Act
        var domain = hydra.ToDlcsModel();

        // Assert
        domain.Id.Should().Be(BatchId);
        domain.Customer.Should().Be(CustomerId);
        domain.Submitted.Should().Be(submitted);
        domain.Count.Should().Be(10);
        domain.Completed.Should().Be(8);
        domain.Errors.Should().Be(2);
        domain.Finished.Should().Be(finished);
    }
}
