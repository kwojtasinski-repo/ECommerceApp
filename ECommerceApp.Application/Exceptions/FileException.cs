using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ECommerceApp.Application.Exceptions
{
    public class FileException : Exception
    {
        public FileException() : base()
        {

        }

        public FileException(string message) : base(message)
        {

        }

        public FileException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public FileException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }
    }
}
