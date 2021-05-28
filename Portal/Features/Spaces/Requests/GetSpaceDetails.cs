using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Newtonsoft.Json.Linq;
using Portal.Legacy;

namespace Portal.Features.Spaces.Requests
{
    /// <summary>
    /// Request to get details of space from API.
    /// </summary>
    /// <remarks>This is temporary to verify API handling</remarks>
    public class GetSpaceDetails : IRequest<JObject>
    {
        public int SpaceId { get; set; }
    }

    public class GetSpaceDetailsHandler : IRequestHandler<GetSpaceDetails, JObject>
    {
        private readonly DlcsClient dlcsClient;

        public GetSpaceDetailsHandler(DlcsClient dlcsClient)
        {
            this.dlcsClient = dlcsClient;
        }
        
        public Task<JObject> Handle(GetSpaceDetails request, CancellationToken cancellationToken)
        {
            return dlcsClient.GetSpaceDetails(request.SpaceId);
        }
    }
}