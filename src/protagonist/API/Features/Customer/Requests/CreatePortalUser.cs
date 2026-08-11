using API.Settings;
using DLCS.Core.Collections;
using DLCS.Core.Encryption;
using DLCS.Core.Strings;
using DLCS.Model.Customers;
using DLCS.Repository;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Features.Customer.Requests;

/// <summary>
/// Create a new Portal user with specified password
/// </summary>
public class CreatePortalUser : IRequest<CreatePortalUserResult>
{
    public required User PortalUser { get; set; }
    public string? Password { get; set; }
    
}

public class CreatePortalUserResult
{
    public bool Conflict { get; set; }
    public User? PortalUser { get; set; }
    public string? Error { get; set; }
}


public class CreatePortalUserHandler : IRequestHandler<CreatePortalUser, CreatePortalUserResult>
{
    private readonly DlcsContext dbContext;
    private readonly IEncryption encryption;
    private readonly ApiSettings settings;

    public CreatePortalUserHandler(
        DlcsContext dbContext,
        IEncryption encryption,
        IOptions<ApiSettings> options)
    {
        this.dbContext = dbContext;
        this.encryption = encryption;
        settings = options.Value;
    }


    public async Task<CreatePortalUserResult> Handle(CreatePortalUser request, CancellationToken cancellationToken)
    {
        const string defaultErrorMessage = "Unable to create user";
        
        if (request.PortalUser.Email.IsNullOrEmpty())
        {
            return new CreatePortalUserResult { Error = "Email address required" };
        }

        if (!request.PortalUser.Email.IsValidEmail())
        {
            return new CreatePortalUserResult { Error = "Email address is invalid" };
        }
        
        var requestEmail = request.PortalUser.Email.ToLower();
        var emailInThisCustomer = await dbContext.Users.AnyAsync(
            user => user.Customer == request.PortalUser.Customer && user.Email.ToLower() == requestEmail,
            cancellationToken);
        if (emailInThisCustomer)
        {
            return new CreatePortalUserResult { Conflict = true, Error = "Portal user already exists." };
        }

        var emailInAnyCustomer = await dbContext.Users.AnyAsync(
            user => user.Email.ToLower() == requestEmail, cancellationToken);
        
        if (emailInAnyCustomer)
        {
            // deliberately opaque: don't reveal that the email is in use by another customer
            return new CreatePortalUserResult { Conflict = true, Error = defaultErrorMessage };
        }

        if (request.Password.IsNullOrEmpty())
        {
            return new CreatePortalUserResult { Error = "Need to set a password when creating a user." };
        }

        if (request.PortalUser.Customer <= 0)
        {
            return new CreatePortalUserResult { Error = "Customer not specified" };
        }

        var newUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Created = DateTime.UtcNow,
            Customer = request.PortalUser.Customer,
            Email = request.PortalUser.Email,
            Enabled = true,
            EncryptedPassword = encryption.Encrypt(string.Concat(settings.LoginSalt, request.Password)),
            Roles = string.Empty
        };

        await dbContext.Users.AddAsync(newUser, cancellationToken);
        var i = await dbContext.SaveChangesAsync(cancellationToken);
        if (i == 1)
        {
            return new CreatePortalUserResult
            {
                PortalUser = new User
                {
                    Id = newUser.Id,
                    Customer = newUser.Customer,
                    Email = newUser.Email,
                    Created = newUser.Created,
                    Enabled = newUser.Enabled
                }
            };
        }

        return new CreatePortalUserResult
        {
            Error = defaultErrorMessage
        };
    }
}
