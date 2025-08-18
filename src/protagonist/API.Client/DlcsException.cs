using System;

namespace API.Client;

public class DlcsException : Exception
{
    public DlcsException()
    {
    }

    public DlcsException(string? message) : base(message)
    {
    }

    public DlcsException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
