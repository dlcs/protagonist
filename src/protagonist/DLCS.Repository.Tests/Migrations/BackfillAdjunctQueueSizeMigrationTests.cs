using DLCS.Model.Processing;
using DLCS.Repository.Migrations;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Migrations;

/// <summary>
/// Verifies the correction SQL applied by the 20260723154327_BackfillAdjunctQueueSize migration (exposed as
/// <see cref="BackfillAdjunctQueueSize.CorrectionSql"/> so this test can't drift from what actually ships).
/// </summary>
[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class BackfillAdjunctQueueSizeMigrationTests
{
    private readonly DlcsContext dbContext;

    public BackfillAdjunctQueueSizeMigrationTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task CorrectionSql_ResetsAdjunctQueueRows_LeavesOtherQueueNamesAlone()
    {
        // Arrange
        await dbContext.Queues.AddAsync(new Queue { Customer = 501, Name = "adjunct", Size = 999 });
        await dbContext.Queues.AddAsync(new Queue { Customer = 502, Name = "adjunct", Size = 12 });
        await dbContext.Queues.AddAsync(new Queue { Customer = 503, Name = "default", Size = 42 });
        await dbContext.Queues.AddAsync(new Queue { Customer = 503, Name = "priority", Size = 7 });
        await dbContext.SaveChangesAsync();

        // Act
        await dbContext.Database.ExecuteSqlRawAsync(BackfillAdjunctQueueSize.CorrectionSql);

        // Assert
        var c501 = await dbContext.Queues.AsNoTracking().SingleAsync(q => q.Customer == 501 && q.Name == "adjunct");
        c501.Size.Should().Be(0);

        var c502 = await dbContext.Queues.AsNoTracking().SingleAsync(q => q.Customer == 502 && q.Name == "adjunct");
        c502.Size.Should().Be(0);

        var c503Default = await dbContext.Queues.AsNoTracking().SingleAsync(q => q.Customer == 503 && q.Name == "default");
        c503Default.Size.Should().Be(42, "non-adjunct queue rows must not be touched");

        var c503Priority = await dbContext.Queues.AsNoTracking().SingleAsync(q => q.Customer == 503 && q.Name == "priority");
        c503Priority.Size.Should().Be(7, "non-adjunct queue rows must not be touched");
    }

    [Fact]
    public async Task CorrectionSql_IsIdempotent_WhenRunTwice()
    {
        // Arrange
        await dbContext.Queues.AddAsync(new Queue { Customer = 601, Name = "adjunct", Size = 999 });
        await dbContext.SaveChangesAsync();

        // Act
        await dbContext.Database.ExecuteSqlRawAsync(BackfillAdjunctQueueSize.CorrectionSql);
        await dbContext.Database.ExecuteSqlRawAsync(BackfillAdjunctQueueSize.CorrectionSql);

        // Assert
        var queue = await dbContext.Queues.AsNoTracking().SingleAsync(q => q.Customer == 601 && q.Name == "adjunct");
        queue.Size.Should().Be(0);
    }
}
