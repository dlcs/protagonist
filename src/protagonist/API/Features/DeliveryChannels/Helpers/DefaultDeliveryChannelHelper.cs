namespace API.Features.DeliveryChannels.Helpers;

public enum SpaceZeroOperation { Create, Modify, Delete }

public static class DefaultDeliveryChannelHelper
{
    /// <summary>
    /// Returns an error message if <paramref name="space"/> is 0, otherwise null.
    /// The <paramref name="operation"/> controls which message is returned.
    /// </summary>
    public static string? GetSpaceZeroErrorMessage(int? space, SpaceZeroOperation operation)
        => space == 0 ? $"Default delivery channels for space 0 cannot be changed for the {operation.ToString().ToLower()} operation" : null;
}
