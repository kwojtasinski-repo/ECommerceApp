using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ECommerceApp.Application.Exceptions
{
    public class BusinessException : System.Exception
    {
        public BusinessException() : base()
        {

        }

        public BusinessException(string message) : base(message)
        {
        
        }

        public BusinessException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public BusinessException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }
    }
}
