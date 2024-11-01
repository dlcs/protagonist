namespace DLCS.Core;

public class ResultMessage<T>
{
    /// <summary>
    /// The associated value.
    /// </summary>
    public T Value { get; }
    
    /// <summary>
    /// The message related to the result
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResultMessage{T}"/> class.
    /// </summary>
    /// <param name="message">A message related to the result</param>
    /// <param name="value">The value.</param>
    public ResultMessage(string message, T value)
    {
        Value = value;
        Message = message;
    }
}