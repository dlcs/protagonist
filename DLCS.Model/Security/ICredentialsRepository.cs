using System.Threading.Tasks;
using DLCS.Model.Customer;

namespace DLCS.Model.Security
{
    public interface ICredentialsRepository
    {
        /// <summary>
        /// Get <see cref="BasicCredentials"/> for specified <see cref="CustomerOriginStrategy"/>.
        /// </summary>
        /// <param name="customerOriginStrategy">The customerOriginStrategy to get credentials for.</param>
        /// <returns><see cref="BasicCredentials"/> object for origin strategy.</returns>
        public Task<BasicCredentials?> GetBasicCredentialsForOriginStrategy(
            CustomerOriginStrategy customerOriginStrategy);
    }
}