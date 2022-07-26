using System;
using System.Runtime.Serialization;

namespace API.Client;

public class DlcsException : Exception
{
    public DlcsException()
    {
    }

    protected DlcsException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public DlcsException(string? message) : base(message)
    {
    }

    public DlcsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}