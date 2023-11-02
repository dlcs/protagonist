using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using API.Client;
using DLCS.Core.Settings;
using MediatR;
using Microsoft.Extensions.Options;

namespace Portal.Features.Batches.Requests;

public class TestBatch : IRequest<bool>
{
    public int BatchId { get; set; }
}

public class TestBatchHandler : IRequestHandler<TestBatch, bool>
{
    private readonly DlcsSettings dlcsSettings;
    private readonly IDlcsClient dlcsClient;

    public TestBatchHandler(IDlcsClient dlcsClient, ClaimsPrincipal currentUser, IOptions<DlcsSettings> dlcsSettings)
    {
        this.dlcsClient = dlcsClient;
        this.dlcsSettings = dlcsSettings.Value;
    }

    public async Task<bool> Handle(TestBatch request, CancellationToken cancellationToken)
    {
        var response = await dlcsClient.TestBatch(request.BatchId);
        return response;
    }
}