﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core;
using DLCS.Core.Caching;
using DLCS.Core.Strings;
using DLCS.Model;
using DLCS.Model.Spaces;
using DLCS.Repository.Entities;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DLCS.Repository.Spaces;

public class SpaceRepository : ISpaceRepository
{
    private readonly DlcsContext dlcsContext;
    private readonly IEntityCounterRepository entityCounterRepository;
    private readonly CacheSettings cacheSettings;
    private readonly IAppCache appCache;
    private readonly ILogger<SpaceRepository> logger;
    
    private const int DefaultMaxUnauthorised = -1;

    public SpaceRepository(
        DlcsContext dlcsContext,
        IOptions<CacheSettings> cacheOptions,
        IAppCache appCache,
        ILogger<SpaceRepository> logger,
        IEntityCounterRepository entityCounterRepository )
    {
        this.dlcsContext = dlcsContext;
        this.appCache = appCache;
        cacheSettings = cacheOptions.Value;
        this.entityCounterRepository = entityCounterRepository;
        this.logger = logger;
    }
    
    public async Task<int?> GetImageCountForSpace(int customerId, int spaceId)
    {
        // NOTE - this is sub-optimal but EntityCounters are not reliable when using PUT
        var count = await dlcsContext.Images.Where(c => c.Customer == customerId && c.Space == spaceId)
            .CountAsync();
        return count;

        /*var entity = await dlcsContext.EntityCounters.AsNoTracking()
            .SingleOrDefaultAsync(ec => ec.Type == "space-images"
                                        && ec.Customer == customerId
                                        && ec.Scope == spaceId.ToString());

        return entity == null ? null : (int) entity.Next;*/
    }

    public Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken)
    {
        return GetSpace(customerId, spaceId, false, cancellationToken);
    }
    
    public async Task<Space?> GetSpace(int customerId, int spaceId, bool noCache, CancellationToken cancellationToken)
    {
        var space = await GetSpaceInternal(customerId, spaceId, cancellationToken, null, true, noCache);
        return space;
    }
    
    public async Task<Space?> GetSpace(int customerId, string name, CancellationToken cancellationToken)
    {
        return await GetSpaceInternal(customerId, -1, cancellationToken, name, noCache: true);
    }

    public async Task<Space> CreateSpace(int customer, string name, string? imageBucket, 
        string[]? tags, string[]? roles, int? maxUnauthorised, CancellationToken cancellationToken)
    {
        int newModelId = await GetIdForNewSpace(customer);
        
        var space = new Space
        {
            Customer = customer,
            Id = newModelId,
            Name = name,
            Created = DateTime.UtcNow,
            ImageBucket = imageBucket ?? string.Empty,
            Tags = tags ?? Array.Empty<string>(),
            Roles = roles ?? Array.Empty<string>(),
            MaxUnauthorised = maxUnauthorised ?? DefaultMaxUnauthorised 
        };

        await dlcsContext.Spaces.AddAsync(space, cancellationToken);
        await entityCounterRepository.TryCreate(customer,  KnownEntityCounters.SpaceImages, space.Id.ToString());
        await dlcsContext.SaveChangesAsync(cancellationToken);
        return space;
    }
    
    private async Task<int> GetIdForNewSpace(int requestCustomer)
    {
        int newModelId;
        Space? existingSpaceInCustomer;
        do
        {
            var next = await entityCounterRepository
                .GetNext(requestCustomer, KnownEntityCounters.CustomerSpaces, requestCustomer.ToString());
            newModelId = Convert.ToInt32(next);
            existingSpaceInCustomer = await dlcsContext.Spaces
                .SingleOrDefaultAsync(s => s.Id == newModelId && s.Customer == requestCustomer);
        } while (existingSpaceInCustomer != null);

        return newModelId;
    }

    private async Task<Space?> GetSpaceInternal(int customerId, int spaceId, 
        CancellationToken cancellationToken, string? name = null,
        bool withApproximateImageCount = false, bool noCache = false)
    {
        var cacheId = name != null ? $"name:{name}" : $"id:{spaceId}";
        
        var key = $"space:{cacheId}";
        if (noCache)
        {
            appCache.Remove(key);
        }
        
        return await appCache.GetOrAddAsync(key, async entry =>
        {
            Space? space;
            if (name != null)
            {
                space = await dlcsContext.Spaces.AsNoTracking()
                    .Where(s => s.Customer == customerId)
                    .SingleOrDefaultAsync(s => s.Name == name, cancellationToken: cancellationToken);
            }
            else
            {
                space = await dlcsContext.Spaces.AsNoTracking().SingleOrDefaultAsync(s =>
                    s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);
            }

            if (space == null || withApproximateImageCount == false)
            {
                return space;
            }
            var counter = await dlcsContext.EntityCounters.AsNoTracking().SingleOrDefaultAsync(ec =>
                ec.Customer == customerId && ec.Type == KnownEntityCounters.SpaceImages &&
                ec.Scope == spaceId.ToString(), cancellationToken: cancellationToken);
            if (counter != null)
            {
                space.ApproximateNumberOfImages = GetApproximateImages(counter.Next);
            }

            return space;
        }, cacheSettings.GetMemoryCacheOptions(CacheDuration.Short));
    }

    public async Task<PageOfSpaces> GetPageOfSpaces(
        int customerId, int page, int pageSize, string orderBy, bool descending, CancellationToken cancellationToken)
    {
        var result = new PageOfSpaces
        {
            Page = page,
            Total = await dlcsContext.Spaces.CountAsync(s => s.Customer == customerId, cancellationToken: cancellationToken),
            Spaces = await dlcsContext.Spaces.AsNoTracking()
                .Where(s => s.Customer == customerId)
                .AsOrderedSpaceQuery(orderBy, descending)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken: cancellationToken)
        };
        // In Deliverator the following is a sub-select. But I suspect that this is not significantly slower.
        var scopes = result.Spaces.Select(s => s.Id.ToString());
        var counters = await dlcsContext.EntityCounters.AsNoTracking()
            .Where(ec => ec.Customer == customerId && ec.Type == KnownEntityCounters.SpaceImages)
            .Where(ec => scopes.Contains(ec.Scope))
            .ToDictionaryAsync(ec => ec.Scope, ec => ec.Next, cancellationToken: cancellationToken);
        foreach (var space in result.Spaces)
        {
            space.ApproximateNumberOfImages = GetApproximateImages(counters[space.Id.ToString()]);
        }

        return result;
    }

    public async Task<Space> PatchSpace(
        int customerId, int spaceId, string? name, int? maxUnauthorised, 
        string[]? tags, string[]? roles, 
        CancellationToken cancellationToken)
    {    
        var keys = new object[] {spaceId, customerId}; // Keys are in this order
        var dbSpace = await dlcsContext.Spaces.FindAsync(keys, cancellationToken);
        if (name.HasText() && name != dbSpace.Name)
        {
            dbSpace.Name = name;
        }

        if (tags != null)
        {
            dbSpace.Tags = tags;
        }

        if (roles != null)
        {
            dbSpace.Roles = roles;
        }

        if (maxUnauthorised != null)
        {
            dbSpace.MaxUnauthorised = (int)maxUnauthorised;
        }

        // ImageBucket?
        
        await dlcsContext.SaveChangesAsync(cancellationToken);

        var retrievedSpace = await GetSpaceInternal(customerId, spaceId, cancellationToken, noCache: true);
        return retrievedSpace;
    }

    public async Task<Space> PutSpace(
        int customerId, int spaceId, string? name, string? imageBucket,
        int? maxUnauthorised, string[]? tags, string[]? roles, 
        CancellationToken cancellationToken)
    {
        var existingSpace = await dlcsContext.Spaces.SingleOrDefaultAsync(s =>
            s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);

        if (existingSpace != null)
        {
            if (name.HasText() && name != existingSpace.Name)
            {
                existingSpace.Name = name;
            }

            if (tags != null)
            {
                existingSpace.Tags = tags;
            }

            if (roles != null)
            {
                existingSpace.Roles = roles;
            }

            if (maxUnauthorised.HasValue)
            {
                existingSpace.MaxUnauthorised = (int)maxUnauthorised;
            }
        }
        else
        {
            var space = new Space
            {
                Customer = customerId,
                Id = spaceId,
                Name = name,
                Created = DateTime.UtcNow,
                ImageBucket = imageBucket ?? string.Empty,
                Tags = tags ?? Array.Empty<string>(),
                Roles = roles ?? Array.Empty<string>(),
                MaxUnauthorised = maxUnauthorised ?? DefaultMaxUnauthorised 
            };
        
            await dlcsContext.Spaces.AddAsync(space, cancellationToken);
            await entityCounterRepository.TryCreate(customerId, KnownEntityCounters.SpaceImages, space.Id.ToString()); 
        }
        
        await dlcsContext.SaveChangesAsync(cancellationToken);
        
        var retrievedSpace = await GetSpaceInternal(customerId, spaceId, cancellationToken, noCache:true);
        return retrievedSpace;
    }

    public async Task<ResultMessage<DeleteResult>> DeleteSpace(int customerId, int spaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var space = await dlcsContext.Spaces.SingleOrDefaultAsync(s =>
                s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);

            if (space == null)
            {
                return new ResultMessage<DeleteResult>($"Space {spaceId} not found", DeleteResult.NotFound);
            }

            var hasImages = await dlcsContext.Images.AnyAsync(i =>
                i.Customer == customerId && i.Space == spaceId, cancellationToken);

            if (hasImages)
            {
                return new ResultMessage<DeleteResult>("Cannot delete a space with images", DeleteResult.Conflict);
            }

            dlcsContext.Spaces.Remove(space);
            await dlcsContext.SaveChangesAsync(cancellationToken);

            await entityCounterRepository.Decrement(customerId, KnownEntityCounters.CustomerSpaces,
                customerId.ToString());
            await entityCounterRepository.Remove(customerId, KnownEntityCounters.SpaceImages,
                space.Id.ToString(), 1);
            return new ResultMessage<DeleteResult>(string.Empty, DeleteResult.Deleted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete space {Customer}/{Space}", customerId, spaceId);
            return new ResultMessage<DeleteResult>("Error deleting space", DeleteResult.Error);
        }
    }

    private static long GetApproximateImages(long entityCounterNext) => Math.Max(entityCounterNext - 1, 0);
}