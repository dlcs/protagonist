using System.Threading.Tasks;
using DLCS.Model.Storage;

namespace DLCS.Repository.Assets
{
    public interface IThumbReorganiser
    {
        Task EnsureNewLayout(ObjectInBucket rootKey);
    }
}