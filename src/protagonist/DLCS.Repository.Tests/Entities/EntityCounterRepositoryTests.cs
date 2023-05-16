﻿using System;
using System.Linq;
using DLCS.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Test.Helpers.Integration;

namespace DLCS.Repository.Tests.Entities;

[Trait("Category", "Database")]
[Collection(DatabaseCollection.CollectionName)]
public class EntityCounterRepositoryTests
{
    private readonly DlcsContext dbContext;
    
    private readonly EntityCounterRepository sut;
    public EntityCounterRepositoryTests(DlcsDatabaseFixture dbFixture)
    {
        dbContext = dbFixture.DbContext;

        sut = new EntityCounterRepository(dbContext);
        
        dbFixture.CleanUp();
    }

    [Fact]
    public async Task Create_AddsEntityCounter()
    {
        // Arrange
        const string scope = nameof(Create_AddsEntityCounter);
        var expected = new EntityCounter
        {
            Customer = 1, Next = 1, Scope = scope, Type = $"{scope}_type"
        };
        
        // Act
        await sut.Create(1, $"{scope}_type", scope);
        
        // Assert
        var saved = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        saved.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task Create_Throws_IfRecordAlreadyExists_SameTypeScopeCustomer()
    {
        // NOTE - this tests behaviour rather than the behaviour being correct
        
        // Arrange
        const string scope = nameof(Create_Throws_IfRecordAlreadyExists_SameTypeScopeCustomer);
        dbContext.EntityCounters.Add(new EntityCounter
        {
            Customer = 1, Next = 9999, Scope = scope, Type = $"{scope}_type"
        });
        await dbContext.SaveChangesAsync();

        // Act
        Func<Task> action = () => sut.Create(1, $"{scope}_type", scope);
        
        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Increment_UpdatesExistingRecord_AndReturnsNextValue()
    {
        // Arrange
        const string scope = nameof(Increment_UpdatesExistingRecord_AndReturnsNextValue);
        dbContext.EntityCounters.Add(new EntityCounter
        {
            Customer = 1, Next = 10, Scope = scope, Type = $"{scope}_type"
        });
        await dbContext.SaveChangesAsync();
        
        // Act
        var next = await sut.Increment(1, $"{scope}_type", scope);
        
        // Assert
        next.Should().Be(11, "returns incremented 'next'");
        
        var dbCounter = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        dbCounter.Next.Should().Be(11, "increments DB 'next'");
    }
    
    [Fact]
    public async Task Increment_CreatesRecord_AndReturnsNextValue()
    {
        // Arrange
        const string scope = nameof(Increment_CreatesRecord_AndReturnsNextValue);

        // Act
        var next = await sut.Increment(1, $"{scope}_type", scope);
        
        // Assert
        next.Should().Be(1);
        
        var dbCounter = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        dbCounter.Next.Should().Be(1);
    }
    
    [Fact]
    public async Task Decrement_UpdatesExistingRecord_AndReturnsNextValue()
    {
        // Arrange
        const string scope = nameof(Decrement_UpdatesExistingRecord_AndReturnsNextValue);
        dbContext.EntityCounters.Add(new EntityCounter
        {
            Customer = 1, Next = 10, Scope = scope, Type = $"{scope}_type"
        });
        await dbContext.SaveChangesAsync();
        
        // Act
        var next = await sut.Decrement(1, $"{scope}_type", scope);
        
        // Assert
        next.Should().Be(9, "returns decremented 'next'");
        
        var dbCounter = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        dbCounter.Next.Should().Be(9, "decremented DB 'next'");
    }
    
    [Fact]
    public async Task Decrement_CreatesRecord_AndReturnsNextValue()
    {
        // Arrange
        const string scope = nameof(Decrement_CreatesRecord_AndReturnsNextValue);

        // Act
        var next = await sut.Decrement(1, $"{scope}_type", scope);
        
        // Assert
        next.Should().Be(0);
        
        var dbCounter = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        dbCounter.Next.Should().Be(0);
    }
    
    // NOTE: These tests verify that repos that use EF and Dapper work nicely with transactions
    [Fact]
    public async Task Decrement_Works_WithinCommittedTransaction()
    {
        // Arrange
        const string scope = nameof(Decrement_Works_WithinCommittedTransaction);

        await using (var tran = await dbContext.Database.BeginTransactionAsync())
        {
            // Act
            var next = await sut.Decrement(1, $"{scope}_type", scope);

            await tran.CommitAsync();
            
            next.Should().Be(0);
        }

        // Assert
        
        var dbCounter = dbContext.EntityCounters.Single(ec => ec.Scope == scope);
        dbCounter.Next.Should().Be(0);
    }
    
    [Fact]
    public async Task Decrement_Works_WithinRolledbackTransaction()
    {
        // Arrange
        const string scope = nameof(Decrement_Works_WithinRolledbackTransaction);

        await using (var tran = await dbContext.Database.BeginTransactionAsync())
        {
            // Act
            var next = await sut.Decrement(1, $"{scope}_type", scope);

            await tran.RollbackAsync();
            
            next.Should().Be(0, "This is not the correct behaviour as transaction rolled back");
        }

        // Assert
        var dbCounter = await dbContext.EntityCounters.SingleOrDefaultAsync(ec => ec.Scope == scope);
        dbCounter.Should().BeNull();
    }
}