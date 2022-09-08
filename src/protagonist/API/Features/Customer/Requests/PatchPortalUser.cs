using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Settings;
using DLCS.Core.Encryption;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Requests;

public class PatchPortalUser : IRequest<PatchPortalUserResult>
{
    public User PortalUser { get; set; }
    public string? Password { get; set; }
}


public class PatchPortalUserResult
{
    public bool Conflict { get; set; }
    public User? PortalUser { get; set; }
    public string? Error { get; set; }
}

public class PatchPortalUserHandler : IRequestHandler<PatchPortalUser, PatchPortalUserResult>
{
    private readonly DlcsContext dbContext;
    private readonly ILogger<PatchPortalUserHandler> logger;
    private readonly IEncryption encryption;
    private readonly ApiSettings settings;

    public PatchPortalUserHandler(
        DlcsContext dbContext,
        ILogger<PatchPortalUserHandler> logger,
        IEncryption encryption,
        IOptions<ApiSettings> options)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.encryption = encryption;
        settings = options.Value;
    }

    public async Task<PatchPortalUserResult> Handle(PatchPortalUser request, CancellationToken cancellationToken)
    {
        var dbUser = await dbContext.Users.FindAsync(new object?[]{request.PortalUser.Id}, cancellationToken);
        if (dbUser == null)
        {
            return new PatchPortalUserResult() { Error = "No such user" };
        }

        if (request.PortalUser.Email.HasText() && request.PortalUser.Email != dbUser.Email)
        {
            var existingUserWithEmail = await dbContext.Users.SingleOrDefaultAsync(
                u => u.Email == request.PortalUser.Email, cancellationToken: cancellationToken);
            if (existingUserWithEmail != null)
            {
                return new PatchPortalUserResult() { Conflict = true, Error = "A user with that email already exists." };
            }
            if (!request.PortalUser.Email.IsValidEmail())
            {
                return new PatchPortalUserResult { Error = "Email address is invalid" };
            }
            dbUser.Email = request.PortalUser.Email;
        }
        if (request.Password.HasText())
        {
            dbUser.EncryptedPassword = encryption.Encrypt(String.Concat(settings.Salt, request.Password));
        }
        
        var i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            return new PatchPortalUserResult
            {
                PortalUser = new User
                {
                    Id = dbUser.Id,
                    Customer = dbUser.Customer,
                    Email = dbUser.Email,
                    Created = dbUser.Created,
                    Enabled = dbUser.Enabled
                }
            };
        }

        return new PatchPortalUserResult
        {
            Error = "Unable to Patch portal user."
        };
    }
}