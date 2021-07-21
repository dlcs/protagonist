using System.Threading.Tasks;
using DLCS.Model.Storage;

namespace DLCS.Repository.Assets
{
    public interface IThumbReorganiser
    {
        /// <summary>
        /// Ensure S3 bucket has new thumbnail layout.
        /// </summary>
        /// <param name="rootKey"><see cref="ObjectInBucket"/> representing root folder for thumbs</param>
        /// <returns><see cref="ReorganiseResult"/> enum representing result</returns>
        Task<ReorganiseResult> EnsureNewLayout(ObjectInBucket rootKey);
    }

    /// <summary>
    /// Default, no-op implementation of <see cref="IThumbReorganiser"/>
    /// </summary>
    public class NonOrganisingReorganiser : IThumbReorganiser
    {
        public Task<ReorganiseResult> EnsureNewLayout(ObjectInBucket rootKey) 
            => Task.FromResult(ReorganiseResult.Unknown);
    }
}