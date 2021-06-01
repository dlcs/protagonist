using System.Threading;
using System.Threading.Tasks;
using API.JsonLd;
using MediatR;
using Portal.Legacy;

namespace Portal.Features.Keys.Commands
{
    /// <summary>
    /// Create a new API key for current customer.
    /// </summary>
    public class CreateNewApiKey : IRequest<ApiKey>
    {
    }
    
    public class CreateNewApiKeyHandler : IRequestHandler<CreateNewApiKey, ApiKey>
    {
        private readonly DlcsClient dlcsClient;

        public CreateNewApiKeyHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public Task<ApiKey> Handle(CreateNewApiKey request, CancellationToken cancellationToken)
        {
            var newApiKey = dlcsClient.CreateNewApiKey();
            return newApiKey;
        }
    }
}