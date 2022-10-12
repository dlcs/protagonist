using System.Runtime.Serialization;

namespace API.Exceptions;

/// <summary>
/// An exception that should result in an HTTP 400 Bad Request exception.
/// </summary>
public class BadRequestException : APIException
{
    public override int? StatusCode => 400;

    public override string Label => "Bad Request";

    public BadRequestException()
    {
    }

    protected BadRequestException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public BadRequestException(string? message) : base(message)
    {
    }

    public BadRequestException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}