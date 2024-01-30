using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ECommerceApp.Application.Exceptions
{
    public class BusinessException : System.Exception
    {
        public string ErrorCode { get; }
        public IDictionary<string, string> Arguments { get; } = new Dictionary<string, string>();

        public BusinessException() : base()
        {

        }

        public BusinessException(string message) : base(message)
        {
        
        }

        public BusinessException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public BusinessException(string message, string errorCode, IDictionary<string, string> arguments) : base(message)
        {
            ErrorCode = errorCode;
            Arguments = arguments;
        }

        public BusinessException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public BusinessException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }
    }
}
