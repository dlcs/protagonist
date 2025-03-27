using LazyCache;
using MediatR;
using Microsoft.Extensions.Logging;

namespace API.Infrastructure.Requests.Pipelines;

/// <summary>
/// Interface for Mediatr requests that invalidate cache records on success 
/// </summary>
public interface IInvalidateCaches
{
    /// <summary>
    /// Collection of cache keys invalidated by successful operation
    /// </summary>
    public string[] InvalidatedCacheKeys { get; }
}

/// <summary>
/// MediatR behaviour that will clear cacheKeys specified in request if request was successful 
/// </summary>
public class CacheInvalidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IInvalidateCaches, IRequest<TResponse>
    where TResponse : IModifyRequest
{
    private readonly IAppCache appCache;
    private readonly ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger;

    public CacheInvalidationBehaviour(IAppCache appCache,
        ILogger<CacheInvalidationBehaviour<TRequest, TResponse>> logger)
    {
        this.appCache = appCache;
        this.logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var nextResponse = await next();

        if (nextResponse.IsSuccess) InvalidateCacheKeys(request);

        return nextResponse;
    }

    private void InvalidateCacheKeys(IInvalidateCaches request)
    {
        foreach (var cacheKey in request.InvalidatedCacheKeys)
        {
            logger.LogDebug("Invalidating cacheKey {CacheKey}", cacheKey);
            appCache.Remove(cacheKey);
        }
    }
}
