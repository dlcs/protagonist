namespace API.Features.DeliveryChannels.Helpers;

public static class DefaultDeliveryChannelHelper
{
    /// <summary>
    /// Returns an error message if <paramref name="space"/> is 0, otherwise null.
    /// </summary>
    public static string? GetSpaceZeroErrorMessage(int? space)
        => space == 0 ? "Default delivery channels for space 0 cannot be changed" : null;
}
