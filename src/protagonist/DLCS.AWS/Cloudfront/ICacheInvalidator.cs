namespace DLCS.AWS.Cloudfront;

public interface ICacheInvalidator
{
    public Task<bool> InvalidateCdnCache(List<string> invalidationPaths, CancellationToken cancellationToken = default);
}