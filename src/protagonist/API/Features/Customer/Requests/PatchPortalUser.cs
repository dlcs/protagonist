using API.Settings;
using DLCS.Core.Encryption;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Requests;

/// <summary>
/// Make a partial update to specified user
/// </summary>
public class PatchPortalUser : IRequest<PatchPortalUserResult>
{
    public required User PortalUser { get; set; }
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
    private readonly IEncryption encryption;
    private readonly ApiSettings settings;

    public PatchPortalUserHandler(
        DlcsContext dbContext,
        IEncryption encryption,
        IOptions<ApiSettings> options)
    {
        this.dbContext = dbContext;
        this.encryption = encryption;
        settings = options.Value;
    }

    public async Task<PatchPortalUserResult> Handle(PatchPortalUser request, CancellationToken cancellationToken)
    {
        const string defaultErrorMessage = "Unable to Patch portal user.";
        
        var dbUser = await dbContext.Users.FindAsync([request.PortalUser.Id], cancellationToken);
        if (dbUser == null || dbUser.Customer != request.PortalUser.Customer)
        {
            return new PatchPortalUserResult { Error = "No such user" };
        }
        
        if (request.PortalUser.Email.HasText() && request.PortalUser.Email != dbUser.Email)
        {
            if (!request.PortalUser.Email.IsValidEmail())
            {
                return new PatchPortalUserResult { Error = "Email address is invalid" };
            }

            var requestEmail = request.PortalUser.Email.ToLower();
            var emailInThisCustomer = await dbContext.Users.AnyAsync(
                u => u.Customer == dbUser.Customer && u.Id != dbUser.Id && u.Email.ToLower() == requestEmail,
                cancellationToken);
            if (emailInThisCustomer)
            {
                return new PatchPortalUserResult { Conflict = true, Error = "Portal user already exists." };
            }

            var emailInAnyCustomer = await dbContext.Users.AnyAsync(
                u => u.Id != dbUser.Id && u.Email.ToLower() == requestEmail, cancellationToken);
            if (emailInAnyCustomer)
            {
                // deliberately opaque: don't reveal that the email is in use by another customer
                return new PatchPortalUserResult { Conflict = true, Error = defaultErrorMessage };
            }

            dbUser.Email = request.PortalUser.Email;
        }
        if (request.Password.HasText())
        {
            dbUser.EncryptedPassword = encryption.Encrypt(String.Concat(settings.LoginSalt, request.Password));
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
            Error = defaultErrorMessage
        };
    }
}
