using Engine.Ingest;

namespace Engine.Tests.Ingest.File;

public class HardcodedAssetIngestorSizeCheckBase : AssetIngestorSizeCheckBase
{
    private readonly int[] noStoragecheckCustomers;

    public HardcodedAssetIngestorSizeCheckBase(params int[] noStoragecheckCustomers)
    {
        this.noStoragecheckCustomers = noStoragecheckCustomers;
    }

    public override bool CustomerHasNoStorageCheck(int customerId)
        => noStoragecheckCustomers.Contains(customerId);
}