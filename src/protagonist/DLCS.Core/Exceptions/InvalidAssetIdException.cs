using System;

namespace DLCS.Core.Exceptions;

/// <summary>
/// Custom exception for handling invalid AssetIds
/// </summary>
public class InvalidAssetIdException : Exception
{
    /// <summary>
    /// ErrorDetail describing type of error
    /// </summary>
    public AssetIdError Error { get; }

    public InvalidAssetIdException(AssetIdError errorDetail)
    {
        Error = errorDetail;
    }

    public InvalidAssetIdException(AssetIdError errorDetail, string? message) : base(message)
    {
        Error = errorDetail;
    }

    public InvalidAssetIdException(AssetIdError errorDetail, string? message, Exception? innerException) : base(message,
        innerException)
    {
        Error = errorDetail;
    }
}

public enum AssetIdError
{
    InvalidFormat,
    TooLong
}