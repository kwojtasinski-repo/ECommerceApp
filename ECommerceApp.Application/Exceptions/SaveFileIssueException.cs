using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ECommerceApp.Application.Exceptions
{
    public class SaveFileIssueException : Exception
    {
        public SaveFileIssueException() : base()
        {

        }

        public SaveFileIssueException(string message) : base(message)
        {

        }

        public SaveFileIssueException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public SaveFileIssueException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }
    }
}
