using DLCS.Model.Processing;
using DLCS.Repository.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Processing;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class CustomerQueueRepositoryTests
{
    private readonly DlcsContext dbContext;
    private readonly CustomerQueueRepository sut;
    public CustomerQueueRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;
        
        // Create a dbContext for use in tests - this has change tracking etc like the normal injected context
        var dlcsContext = new DlcsContext(
            new DbContextOptionsBuilder<DlcsContext>()
                .UseNpgsql(dbFixture.ConnectionString).Options
        );
        
        sut = new CustomerQueueRepository(dlcsContext, new NullLogger<CustomerQueueRepository>());
        
        dbFixture.CleanUp();
    }
    
    [Fact]
    public async Task IncrementSize_CreatesQueueOfSize_IfNotExists()
    {
        const string queueName = nameof(IncrementSize_CreatesQueueOfSize_IfNotExists);
        await sut.IncrementSize(99, queueName, 123);

        var createdQueue = await dbContext.Queues.SingleAsync(q => q.Name == queueName && q.Customer == 99);
        createdQueue.Size.Should().Be(123);
    }
    
    [Fact]
    public async Task IncrementSize_UpdatesQueueSize_IfExists()
    {
        const string queueName = nameof(IncrementSize_UpdatesQueueSize_IfExists);
        await dbContext.Queues.AddAsync(new Queue { Customer = 99, Name = queueName, Size = 100 });
        await dbContext.SaveChangesAsync();
        
        await sut.IncrementSize(99, queueName, 123);

        var createdQueue = await dbContext.Queues.SingleAsync(q => q.Name == queueName && q.Customer == 99);
        createdQueue.Size.Should().Be(223);
    }
    
    [Fact]
    public async Task DecrementSize_CreatesQueueOfSize0_IfNotExists()
    {
        const string queueName = nameof(DecrementSize_CreatesQueueOfSize0_IfNotExists);
        await sut.DecrementSize(99, queueName, 123);

        var createdQueue = await dbContext.Queues.SingleAsync(q => q.Name == queueName && q.Customer == 99);
        createdQueue.Size.Should().Be(0);
    }
    
    [Fact]
    public async Task DecrementSize_UpdatesQueueSize_IfExists()
    {
        const string queueName = nameof(DecrementSize_UpdatesQueueSize_IfExists);
        await dbContext.Queues.AddAsync(new Queue { Customer = 99, Name = queueName, Size = 100 });
        await dbContext.SaveChangesAsync();
        
        await sut.DecrementSize(99, queueName, 20);

        var createdQueue = await dbContext.Queues.SingleAsync(q => q.Name == queueName && q.Customer == 99);
        createdQueue.Size.Should().Be(80);
    }
    
    [Fact]
    public async Task DecrementSize_UpdatesQueueSizeTo0_IfExists_AndDecrementWouldMakeSizeNegative()
    {
        const string queueName = nameof(DecrementSize_UpdatesQueueSizeTo0_IfExists_AndDecrementWouldMakeSizeNegative);
        await dbContext.Queues.AddAsync(new Queue { Customer = 99, Name = queueName, Size = 100 });
        await dbContext.SaveChangesAsync();
        
        await sut.DecrementSize(99, queueName, 120);

        var createdQueue = await dbContext.Queues.SingleAsync(q => q.Name == queueName && q.Customer == 99);
        createdQueue.Size.Should().Be(0);
    }
}