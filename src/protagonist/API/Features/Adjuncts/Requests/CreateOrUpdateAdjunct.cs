using API.Infrastructure.Requests;
using DLCS.Core;
using DLCS.Model.Assets;
using DLCS.Repository;
using DLCS.Repository.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace API.Features.Adjuncts.Requests;

public class CreateOrUpdateAdjunct(Adjunct adjunct) : IRequest<ModifyEntityResult<Adjunct>>
{
    public Adjunct Adjunct { get; set; } = adjunct;
}

public class CreateOrUpdateAdjunctHandler(DlcsContext dbContext)
    : IRequestHandler<CreateOrUpdateAdjunct, ModifyEntityResult<Adjunct>>
{
    public async Task<ModifyEntityResult<Adjunct>> Handle(CreateOrUpdateAdjunct request, CancellationToken cancellationToken)
    {
        request.Adjunct.Created = DateTime.UtcNow;
        request.Adjunct.Modified = DateTime.UtcNow;
        
        await dbContext.Adjuncts.AddAsync(request.Adjunct, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetDatabaseError() is UniqueConstraintError) // todo: update as well
        {
            return ModifyEntityResult<Adjunct>.Failure(
                $"An adjunct called '{request.Adjunct.Id}' already exists",
                WriteResult.Conflict);
        }

        return ModifyEntityResult<Adjunct>.Success(request.Adjunct, WriteResult.Created);
    }
}
