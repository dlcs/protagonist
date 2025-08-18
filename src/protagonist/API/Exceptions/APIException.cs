namespace API.Exceptions;

public class APIException : Exception
{
    public virtual int? StatusCode { get; set; }
    
    public virtual string Label { get; set; }
    
    public APIException()
    {
    }

    public APIException(string? message) : base(message)
    {
    }

    public APIException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
