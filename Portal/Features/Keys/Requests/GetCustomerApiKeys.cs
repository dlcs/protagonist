using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using MediatR;

namespace Portal.Features.Keys.Requests
{
    /// <summary>
    /// Get all ApiKeys for current customer.
    /// </summary>
    public class GetCustomerApiKeys : IRequest<IEnumerable<string>?>
    {
    }

    public class GetCustomerApiKeysHandler : IRequestHandler<GetCustomerApiKeys, IEnumerable<string>?>
    {
        private readonly IDlcsClient dlcsClient;

        public GetCustomerApiKeysHandler(IDlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public async Task<IEnumerable<string>?> Handle(GetCustomerApiKeys request, CancellationToken cancellationToken)
        {
            var apiKeys = await dlcsClient.GetApiKeys();
            return apiKeys;
        }
    }
}