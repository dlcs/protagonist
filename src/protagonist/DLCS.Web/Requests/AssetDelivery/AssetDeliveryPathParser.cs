using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DLCS.Core.Exceptions;
using DLCS.Model.PathElements;
using IIIF.ImageApi;
using Microsoft.Extensions.Logging;

namespace DLCS.Web.Requests.AssetDelivery;

/// <summary>
/// Contains operations for parsing asset delivery requests.
/// </summary>
public interface IAssetDeliveryPathParser
{
    /// <summary>
    /// Parse asset delivery requests to <see cref="BaseAssetRequest"/>
    /// </summary>
    /// <param name="path">Full asset request path</param>
    /// <typeparam name="T">Type of <see cref="BaseAssetRequest"/> to return.</typeparam>
    /// <returns>Populated <see cref="ImageAssetDeliveryRequest"/> object</returns>
    Task<T> Parse<T>(string path) where T : BaseAssetRequest, new();
    
    /// <summary>
    /// Parse asset delivery requests to <see cref="BaseAssetRequest"/>, any exceptions thrown during parsing are
    /// converted to an HttpException for ease of handling.
    /// </summary>
    /// <param name="path">Full asset request path</param>
    /// <typeparam name="T">Type of <see cref="BaseAssetRequest"/> to return.</typeparam>
    /// <returns>Populated <see cref="ImageAssetDeliveryRequest"/> object</returns>
    /// <exception cref="HttpException">Raised with appropriate status code depending on error</exception>
    Task<T> ParseForHttp<T>(string path) where T : BaseAssetRequest, new();
}

public class AssetDeliveryPathParser : IAssetDeliveryPathParser
{
    private readonly IPathCustomerRepository pathCustomerRepository;
    private readonly ILogger<AssetDeliveryPathParser> logger;

    // regex to clean query params and hashmark from asset id
    private static readonly Regex AssetIdClean = new(@"[\?\#].*$", RegexOptions.Compiled);
    
    // regex to match v1, v2 etc but not a v23
    private static readonly Regex VersionRegex = new("^(v\\d)$", RegexOptions.Compiled);

    public AssetDeliveryPathParser(IPathCustomerRepository pathCustomerRepository,
        ILogger<AssetDeliveryPathParser> logger)
    {
        this.pathCustomerRepository = pathCustomerRepository;
        this.logger = logger;
    }

    public async Task<T> ParseForHttp<T>(string path)
        where T : BaseAssetRequest, new()
    {
        try
        {
            return await Parse<T>(path);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogError(ex, "Could not find Customer/Space from '{Path}'", path);
            throw new HttpException(HttpStatusCode.NotFound, $"Could not find Customer/Space from '{path}'", ex);
        }
        catch (FormatException ex)
        {
            logger.LogError(ex, "Error parsing path '{Path}'", path);
            throw new HttpException(HttpStatusCode.BadRequest, $"Error parsing path '{path}'", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error parsing path '{Path}'", path);
            throw new HttpException(HttpStatusCode.BadRequest, $"Error parsing path '{path}'", ex);
        }
    }

    public async Task<T> Parse<T>(string path) 
        where T : BaseAssetRequest, new()
    {
        var assetRequest = new T();
        var escapedPath = WebUtility.UrlDecode(path);
        await ParseBaseAssetRequest(escapedPath, assetRequest);

        if (assetRequest is ImageAssetDeliveryRequest imageAssetRequest)
        {
            imageAssetRequest.IIIFImageRequest = ImageRequest.Parse(escapedPath, assetRequest.BasePath, true);
        }
        else if (assetRequest is TimeBasedAssetDeliveryRequest timebasedAssetRequest)
        {
            timebasedAssetRequest.TimeBasedRequest = timebasedAssetRequest.AssetPath.Replace(assetRequest.AssetId, string.Empty);
        }

        return assetRequest;
    }

    private async Task ParseBaseAssetRequest(string path, BaseAssetRequest request)
    {
        const int routeIndex = 0;
        const int defaultCustomerIndex = 1;
        const int defaultSpacesIndex = 2;

        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // The first slug after prefix is generally customer but it might be version number
        // If the latter, offset standard indexes by 1
        var versionCandidate = parts[defaultCustomerIndex];
        var isVersioned = VersionRegex.IsMatch(versionCandidate);
        var versionOffset = isVersioned ? 1 : 0;
        
        request.RoutePrefix = parts[routeIndex];

        if (isVersioned)
        {
            request.VersionPathValue = versionCandidate;
            request.VersionedRoutePrefix = $"{request.RoutePrefix}/{versionCandidate}";
        }
        else
        {
            request.VersionedRoutePrefix = request.RoutePrefix;
        }

        request.CustomerPathValue = parts[defaultCustomerIndex + versionOffset];
        var space = parts[defaultSpacesIndex + versionOffset];
        request.Space = int.Parse(space);
        request.AssetPath = GetAssetPath(parts, versionOffset);
        request.AssetId = request.AssetPath.Split("/")[0];
        request.BasePath =
            GenerateBasePath(request.RoutePrefix, request.CustomerPathValue, space, isVersioned, versionCandidate);

        request.Customer = await pathCustomerRepository.GetCustomerPathElement(request.CustomerPathValue);

        request.NormalisedBasePath =
            GenerateBasePath(request.RoutePrefix, request.Customer.Id.ToString(), space, isVersioned,
                versionCandidate);
        request.NormalisedFullPath = string.Concat(request.NormalisedBasePath, request.AssetPath);
    }

    private static string GetAssetPath(string[] parts, int versionOffset)
    {
         var assetPath = string.Join("/", parts.Skip(3 + versionOffset));
         return AssetIdClean.Replace(assetPath, string.Empty);
    }

    private static string GenerateBasePath(string route, string customer, string space, bool isVersioned,
        string? version)
    {
        // Get the full length of the final path 
        var numberOfSlashes = isVersioned ? 5 : 4;
        var outputLength = route.Length + customer.Length + space.Length + numberOfSlashes +
                           (isVersioned ? version.Length : 0);
        
        var stringBuilder = new StringBuilder("/", outputLength).Append(route).Append('/');

        if (isVersioned) stringBuilder.Append(version).Append('/');

        return stringBuilder
            .Append(customer)
            .Append('/')
            .Append(space)
            .Append('/')
            .ToString();
    }
}
