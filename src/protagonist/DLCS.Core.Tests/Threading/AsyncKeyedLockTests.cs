using AsyncKeyedLock;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DLCS.Core.Tests.Threading;

[Trait("Category", "Manual")]
public class AsyncKeyedLockTests
{
    private readonly AsyncKeyedLocker<string> sut;

    public AsyncKeyedLockTests()
    {
        sut = new AsyncKeyedLocker<string>();
    }
    
    [Fact]
    public async Task LockAsync_PreventsItemsWithSameKey_AttainingLockAtSameTime_FromDifferentInstances()
    {
        const string key = nameof(LockAsync_PreventsItemsWithSameKey_AttainingLockAtSameTime_FromDifferentInstances);
        var calls = new List<string>();
        bool task1Complete = false;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                // Task will get lock immediately but wait 400ms to release
                using (var theLock = await new AsyncKeyedLocker<string>().LockAsync(key))
                {
                    await Task.Delay(400);
                    calls.Add("Quick attain, run slow");
                    task1Complete = true;
                }
            }),
            Task.Run(async () =>
            {
                // Task will try get lock after 200ms 
                await Task.Delay(200);
                using (var theLock = await new AsyncKeyedLocker<string>().LockAsync(key))
                {
                    calls.Add("Slow attain, run quick");
                    task1Complete.Should().BeTrue();
                }
            }));

        calls.Count.Should().Be(2);
        calls[0].Should().Be("Quick attain, run slow");
        calls[1].Should().Be("Slow attain, run quick");
    }

    [Fact]
    public async Task LockAsync_PreventsItemsWithSameKey_AttainingLockAtSameTime()
    {
        const string key = nameof(LockAsync_PreventsItemsWithSameKey_AttainingLockAtSameTime);
        var calls = new List<string>();
        bool task1Complete = false;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                // Task will get lock immediately but wait 400ms to release
                using (var theLock = await sut.LockAsync(key))
                {
                    await Task.Delay(400);
                    calls.Add("Quick attain, run slow");
                    task1Complete = true;
                }
            }),
            Task.Run(async () =>
            {
                // Task will try get lock after 200ms 
                await Task.Delay(200);
                using (var theLock = await sut.LockAsync(key))
                {
                    calls.Add("Slow attain, run quick");
                    task1Complete.Should().BeTrue();
                }
            }));

        calls.Count.Should().Be(2);
        calls[0].Should().Be("Quick attain, run slow");
        calls[1].Should().Be("Slow attain, run quick");
    }

    [Fact]
    public async Task LockAsync_AllowsItemsWithDifferentKey_AttainingLockAtSameTime()
    {
        const string key = nameof(LockAsync_AllowsItemsWithDifferentKey_AttainingLockAtSameTime);
        var calls = new List<string>();
        bool task1Complete = false;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                // Task will get lock immediately but wait 400ms to release
                using (var theLock = await sut.LockAsync(key))
                {
                    await Task.Delay(400);
                    calls.Add("Quick attain, run slow");
                    task1Complete = true;
                }
            }),
            Task.Run(async () =>
            {
                // Task will try get lock after 200ms, different key so should get it
                await Task.Delay(200);
                using (var theLock = await sut.LockAsync($"not{key}"))
                {
                    calls.Add("Slow attain, run quick");
                    task1Complete.Should().BeFalse();
                }
            }));

        calls.Count.Should().Be(2);
        calls[0].Should().Be("Slow attain, run quick");
        calls[1].Should().Be("Quick attain, run slow");
    }

    [Fact]
    public async Task LockAsyncTimespan_CanAttainingLockAfterTimeout_MarkedAsNotLocked()
    {
        const string key = nameof(LockAsyncTimespan_CanAttainingLockAfterTimeout_MarkedAsNotLocked);
        var calls = new List<string>();
        bool task1Complete = false;
        bool task2Complete = false;

        await Task.WhenAll(
            Task.Run(async () =>
            {
                // Task will get lock immediately but wait 400ms to release
                using (var theLock = await sut.LockAsync(key))
                {
                    await Task.Delay(400);
                    calls.Add("Quick attain, run slow");
                    task1Complete = true;
                }
            }),
            Task.Run(async () =>
            {
                // Task will try get lock after 200ms, wait 100ms then enter block even if lock not attained
                await Task.Delay(200);
                using (var theLock = await sut.LockAsync(key, TimeSpan.FromMilliseconds(100)))
                {
                    calls.Add("Slow attain, run quick");
                    task1Complete.Should().BeFalse();
                }
            }),
            Task.Run(async () =>
            {
                // Task will try get lock after 600ms
                await Task.Delay(600);
                using (var theLock = await sut.LockAsync(key))
                {
                    calls.Add("Verify timeout lock doesn't affect normal process");
                    task2Complete.Should().BeFalse();
                }
            }));

        calls.Count.Should().Be(3);
        calls[0].Should().Be("Slow attain, run quick");
        calls[1].Should().Be("Quick attain, run slow");
        calls[2].Should().Be("Verify timeout lock doesn't affect normal process");
    }

    [Fact]
    public async Task CanGetNestedLockForDifferentKey()
    {
        var calls = new List<string>();

        using (var firstLock = await sut.LockAsync("first"))
        {
            calls.Add("Attained first");
            using (var secondLock = await sut.LockAsync("second"))
            {
                calls.Add("Attained second");
            }
            calls.Add("Released second");
        }
        
        calls.Count.Should().Be(3);
        calls[0].Should().Be("Attained first");
        calls[1].Should().Be("Attained second");
        calls[2].Should().Be("Released second");
    }
}