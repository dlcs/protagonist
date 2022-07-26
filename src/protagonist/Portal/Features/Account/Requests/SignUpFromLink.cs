using System;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.HydraModel;
using DLCS.Repository;
using DLCS.Web.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portal.Settings;
using Serilog;

namespace Portal.Features.Account.Requests;

public class SignUpFromLink : IRequest<SignupFromLinkResult>
{
    public string SignUpCode { get; set; }
    public string CustomerSlugName { get; set; }
    public string CustomerDisplayName { get; set; }
    public string UserEmail { get; set; }
    public string UserPassword { get; set; }
}

public class SignupFromLinkResult
{
    public Customer? Customer { get; set; }
    public PortalUser? PortalUser { get; set; }
    public ApiKey? ApiKey { get; set; }
    public string? Message { get; set; }

    public int? GetCustomerId()
    {
        return Customer?.GetLastPathElementAsInt();
    }
}

public class SignUpFromLinkHandler : IRequestHandler<SignUpFromLink, SignupFromLinkResult>
{
    private readonly ILogger<SignUpFromLinkHandler> logger;
    private readonly PortalSettings options;
    private readonly DeliveratorApiAuth deliveratorApiAuth;
    private readonly DlcsContext dbContext;
    private readonly AdminDlcsClient adminDlcsClient;
    
    public SignUpFromLinkHandler(
        ILogger<SignUpFromLinkHandler> logger,
        IOptions<PortalSettings> options,
        DeliveratorApiAuth deliveratorApiAuth,
        DlcsContext dbContext,
        AdminDlcsClient adminDlcsClient)
    {
        this.logger = logger;
        this.options = options.Value;
        this.deliveratorApiAuth = deliveratorApiAuth;
        this.dbContext = dbContext;
        this.adminDlcsClient = adminDlcsClient;
    }

    public async Task<SignupFromLinkResult> Handle(SignUpFromLink request, CancellationToken cancellationToken)
    {
        var admin = await dbContext.Customers.FirstAsync(c => c.Administrator, cancellationToken: cancellationToken);
        var basicAuth = deliveratorApiAuth.GetBasicAuthForCustomer(admin, options.ApiSalt);
        // This isn't right... the adminDlcsClient should come equipped with an admin-level httpclient
        // do we want to do that in startup though? We don't have the admin customer there.
        adminDlcsClient.SetBasicAuth(basicAuth);
        var result = new SignupFromLinkResult();
        logger.LogInformation("Creating customer " + request.CustomerDisplayName);
        var requestCustomer = new Customer
        {
            Name = request.CustomerSlugName, 
            DisplayName = request.CustomerDisplayName
        };
        try
        {
            result.Customer = await adminDlcsClient.CreateCustomer(requestCustomer);
        }
        catch (Exception e)
        {
            Log.Error("Could not create customer", e);
            result.Message = "Unable to create this customer";
        }
        if (result.Customer == null || !string.IsNullOrWhiteSpace(result.Message))
        {
            return result;
        }
        logger.LogInformation("Customer created: " + result.Customer.Id);
        logger.LogInformation("Creating portal user " + request.CustomerDisplayName);
        var reqPortalUser = new PortalUser
        {
            Email = request.UserEmail,
            Password = request.UserPassword,
            Enabled = true
        };
        try
        {
            result.PortalUser = await adminDlcsClient.CreatePortalUser(reqPortalUser, result.Customer.Id);
        }
        catch (Exception e)
        {
            Log.Error("Could not create portal user", e);
            result.Message = "Unable to create this user";
        }
        if (result.PortalUser == null  || !string.IsNullOrWhiteSpace(result.Message))
        {
            return result;
        }
        logger.LogInformation("Portal user created: " + result.PortalUser.Email);
        logger.LogInformation("Creating API key");
        try
        {
            result.ApiKey = await adminDlcsClient.CreateNewApiKey(result.Customer.Id);
        }
        catch (Exception e)
        {
            Log.Error("Could not create API key for customer", e);
            result.Message = "Unable to create API key";
        }

        if (result.ApiKey == null)
        {
            return result;
        }

        var link = await dbContext.SignupLinks.FindAsync(request.SignUpCode);
        link.CustomerId = result.GetCustomerId();
        await dbContext.SaveChangesAsync(cancellationToken);
        
        result.Message = "Success";
        return result;
    }
}