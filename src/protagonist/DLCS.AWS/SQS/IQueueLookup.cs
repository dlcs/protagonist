using DLCS.Model.Assets;

namespace DLCS.AWS.SQS;

public interface IQueueLookup
{
    /// <summary>
    /// Get the name of queue used for processing assets of specified family
    /// </summary>
    string GetQueueNameForFamily(AssetFamily family, bool priority = false);
}