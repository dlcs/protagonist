using System;
using System.Runtime.Serialization;

namespace API
{
    public class APIException : Exception
    {
        public APIException()
        {
        }

        protected APIException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public APIException(string? message) : base(message)
        {
        }

        public APIException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}