using Dapper;
using DLCS.Repository;
using Microsoft.Extensions.Logging;

namespace API.Features.Customer;

/// <summary>
/// Class responsible for setting new customer up with DeliveryChannelPolicies and DefaultDeliveryChannels
/// </summary>
public class DapperNewCustomerDeliveryChannelRepository : IDapperContextRepository
{
    private readonly ILogger<DapperNewCustomerDeliveryChannelRepository> logger;
    public DlcsContext DlcsContext { get; }

    public DapperNewCustomerDeliveryChannelRepository(DlcsContext dlcsContext,
        ILogger<DapperNewCustomerDeliveryChannelRepository> logger)
    {
        this.logger = logger;
        DlcsContext = dlcsContext;
    }

    public async Task<bool> SeedDeliveryChannelsData(int newCustomerId)
    {
        try
        {
            var conn = await DlcsContext.GetOpenNpgSqlConnection();

            await conn.ExecuteAsync(CreateDeliveryChannelSql, new { Customer = newCustomerId });
            await conn.ExecuteAsync(CreateDefaultDeliveryChannelSql, new { Customer = newCustomerId });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing DeliveryChannel creation scripts for customer: {CustomerId}",
                newCustomerId);
            return false;
        }
    }
    
    private const string CreateDeliveryChannelSql = @"
insert into ""DeliveryChannelPolicies"" (""Name"", ""DisplayName"", ""Customer"", ""Channel"", ""System"", ""Created"", ""Modified"",
                                        ""PolicyData"")
select dcp.""Name"",
       dcp.""DisplayName"",
       @Customer,
       dcp.""Channel"",
       dcp.""System"",
       dcp.""Modified"",
       dcp.""Modified"",
       dcp.""PolicyData""
from ""DeliveryChannelPolicies"" dcp
where dcp.""Customer"" = 1
  and dcp.""System"" = false;
";

    private const string CreateDefaultDeliveryChannelSql = @"
insert into ""DefaultDeliveryChannels"" (""Id"", ""Customer"", ""Space"", ""MediaType"", ""DeliveryChannelPolicyId"")
select gen_random_uuid(),
       @Customer,
       0,
       ddc.""MediaType"",
       case
           when ddc.""DeliveryChannelPolicyId"" = 5 then (select ""Id"" from ""DeliveryChannelPolicies"" where ""Channel"" = 'iiif-av' and ""Name"" = 'default-audio' and ""Customer"" = @Customer)
           when ddc.""DeliveryChannelPolicyId"" = 6 then (select ""Id"" from ""DeliveryChannelPolicies"" where ""Channel"" = 'iiif-av' and ""Name"" = 'default-video' and ""Customer"" = @Customer)
           when ddc.""DeliveryChannelPolicyId"" = 3 then (select ""Id"" from ""DeliveryChannelPolicies"" where ""Channel"" = 'thumbs' and ""Name"" = 'default' and ""Customer"" = @Customer)
           else ""DeliveryChannelPolicyId""
       end as policyId
from ""DefaultDeliveryChannels"" ddc
where ddc.""Customer"" = 1
  and ddc.""Space"" = 0;
";
}