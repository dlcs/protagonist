namespace DLCS.Core;

public class ResultStatus<T>
{
    /// <summary>
    /// Indicates whether the operation was successful
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The associated value.
    /// </summary>
    public T? Value { get; }
    
    /// <summary>
    /// Optional code for error
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}"/> class.
    /// </summary>
    /// <param name="success">if set to <c>true</c> [success].</param>
    public ResultStatus(bool success)
    {
        Success = success;
        Value = default!;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}"/> class.
    /// </summary>
    /// <param name="success">if set to <c>true</c> [success].</param>
    /// <param name="value">The value.</param>
    public ResultStatus(bool success, T value)
    {
        Success = success;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultStatus{T}"/> class.
    /// </summary>
    /// <param name="success">if set to <c>true</c> [success].</param>
    /// <param name="value">The value.</param>
    /// <param name="errorCode">Error code, optional for failed requests</param>
    public ResultStatus(bool success, T value, int? errorCode)
    {
        Success = success;
        Value = value;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an unsuccessful result with specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="errorCode">Optional error code</param>
    public static ResultStatus<T> Unsuccessful(T value, int? errorCode = null) => new(false, value, errorCode);

    /// <summary>
    /// Creates a successful result with specified value
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns></returns>
    public static ResultStatus<T> Successful(T value) => new(true, value);
}