using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ECommerceApp.Infrastructure.Exceptions
{
    public class InfrastructureException : System.Exception
    {
        public InfrastructureException() : base()
        {

        }

        public InfrastructureException(string message) : base(message)
        {

        }

        public InfrastructureException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public InfrastructureException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }
    }
}
