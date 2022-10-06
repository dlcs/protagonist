using MediatR;

namespace API.Features.NamedQueries.Requests;

/// <summary>
/// Handler for deleting PDF for named query
/// </summary>
public class DeletePdf : IRequest<bool?>
{
    public string QueryName { get; }
    public string Args { get; }

    public DeletePdf(string queryName, string args)
    {
        QueryName = queryName;
        Args = args;
    }
}

