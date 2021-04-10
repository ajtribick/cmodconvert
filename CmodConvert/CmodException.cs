using System;
using System.Runtime.Serialization;

namespace CmodConvert
{
    [Serializable]
    public class CmodException : Exception
    {
        public CmodException() { }

        public CmodException(string? message) : base(message) { }

        public CmodException(string? message, Exception? innerException) : base(message, innerException) { }

        public CmodException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
