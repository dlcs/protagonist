﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLCS.Core.Strings;
using DLCS.Model;
using DLCS.Model.Spaces;
using Microsoft.EntityFrameworkCore;

namespace DLCS.Repository.Spaces
{
    public class SpaceRepository : ISpaceRepository
    {
        private readonly DlcsContext dlcsContext;
        private readonly IEntityCounterRepository entityCounterRepository;

        public SpaceRepository(
            DlcsContext dlcsContext,
            IEntityCounterRepository entityCounterRepository )
        {
            this.dlcsContext = dlcsContext;
            this.entityCounterRepository = entityCounterRepository;
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

        public async Task<Space?> GetSpace(int customerId, int spaceId, CancellationToken cancellationToken)
        {
            var space = await GetSpaceInternal(customerId, spaceId, cancellationToken);
            return space;
        }
        
        public async Task<Space?> GetSpace(int customerId, string name, CancellationToken cancellationToken)
        {
            var space = await GetSpaceInternal(customerId, -1, cancellationToken, name);
            return space;
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
                Created = DateTime.Now,
                ImageBucket = imageBucket,
                Tags = tags ?? Array.Empty<string>(),
                Roles = roles ?? Array.Empty<string>(),
                MaxUnauthorised = maxUnauthorised ?? -1
            };

            await dlcsContext.Spaces.AddAsync(space, cancellationToken);
            await entityCounterRepository.Create(customer,  "space-images", space.Id.ToString());
            await dlcsContext.SaveChangesAsync(cancellationToken);
            return space;
        }
        
        private async Task<int> GetIdForNewSpace(int requestCustomer)
        {
            int newModelId;
            Space existingSpaceInCustomer;
            do
            {
                var next = await entityCounterRepository
                    .GetNext(requestCustomer, "space", requestCustomer.ToString());
                newModelId = Convert.ToInt32(next);
                existingSpaceInCustomer = await dlcsContext.Spaces
                    .SingleOrDefaultAsync(s => s.Id == newModelId && s.Customer == requestCustomer);
            } while (existingSpaceInCustomer != null);

            return newModelId;
        }
        

        private async Task<Space?> GetSpaceInternal(int customerId, int spaceId, 
            CancellationToken cancellationToken, string? name = null)
        {
            Space space;
            if (name != null)
            {
                space = await dlcsContext.Spaces
                    .Where(s => s.Customer == customerId)
                    .SingleOrDefaultAsync(s => s.Name == name, cancellationToken: cancellationToken);
            }
            else
            {
                space = await dlcsContext.Spaces.AsNoTracking().SingleOrDefaultAsync(s =>
                    s.Customer == customerId && s.Id == spaceId, cancellationToken: cancellationToken);
            }
            var counter = await dlcsContext.EntityCounters.AsNoTracking().SingleOrDefaultAsync(ec =>
                ec.Customer == customerId && ec.Type == "space-images" &&
                ec.Scope == spaceId.ToString(), cancellationToken: cancellationToken);
            if (space != null && counter != null)
            {
                space.ApproximateNumberOfImages = counter.Next;
            }

            return space;
        }

        public async Task<PageOfSpaces> GetPageOfSpaces(int customerId, int page, int pageSize, CancellationToken cancellationToken)
        {
            var result = new PageOfSpaces
            {
                Page = page,
                Total = await dlcsContext.Spaces.CountAsync(s => s.Customer == customerId, cancellationToken: cancellationToken),
                Spaces = await dlcsContext.Spaces.AsNoTracking()
                    .Where(s => s.Customer == customerId)
                    .OrderBy(s => s.Id) // TODO - use request.OrderBy
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken: cancellationToken)
            };
            // In Deliverator the following is a sub-select. But I suspect that this is not significantly slower.
            var scopes = result.Spaces.Select(s => s.Id.ToString());
            var counters = await dlcsContext.EntityCounters.AsNoTracking()
                .Where(ec => ec.Customer == customerId && ec.Type == "space-images")
                .Where(ec => scopes.Contains(ec.Scope))
                .ToDictionaryAsync(ec => ec.Scope, ec => ec.Next, cancellationToken: cancellationToken);
            foreach (var space in result.Spaces)
            {
                space.ApproximateNumberOfImages = counters[space.Id.ToString()];
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

            var retrievedSpace = await GetSpace(customerId, spaceId, cancellationToken);
            return retrievedSpace;
        }
    }
}