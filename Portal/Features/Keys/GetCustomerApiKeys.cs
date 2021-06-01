using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Portal.Legacy;

namespace Portal.Features.Keys
{
    /// <summary>
    /// Get all ApiKeys for current customer.
    /// </summary>
    public class GetCustomerApiKeys : IRequest<IEnumerable<string>?>
    {
    }

    public class GetCustomerApiKeysHandler : IRequestHandler<GetCustomerApiKeys, IEnumerable<string>?>
    {
        private readonly DlcsClient dlcsClient;

        public GetCustomerApiKeysHandler(DlcsClient dlcsClient)
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