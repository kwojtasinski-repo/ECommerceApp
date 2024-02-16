using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ECommerceApp.Application.Exceptions
{
    public class BusinessException : Exception
    {
        public string ErrorCode { get; }
        public IDictionary<string, string> Arguments { get; } = new Dictionary<string, string>();
        public IEnumerable<ErrorCode> Codes => _codes;
        public List<ErrorCode> _codes = new ();

        public BusinessException() : base()
        {

        }

        public BusinessException(string message) : base(message)
        {
        
        }

        public BusinessException(string message, ErrorCode code) : base(message)
        {
            ValidateCode(code);
            _codes.Add(code);
        }

        public BusinessException(string message, System.Exception exception) : base(message, exception)
        {

        }

        public BusinessException(SerializationInfo serialization, StreamingContext streamingContext) : base(serialization, streamingContext)
        {

        }

        public BusinessException(ErrorMessage errorMessage) : base(errorMessage.Message.ToString())
        {
            _codes = errorMessage.ErrorCodes ?? new List<ErrorCode>();
        }

        public BusinessException AddCode(string code)
        {
            _codes.Add(Exceptions.ErrorCode.Create(code));
            return this;
        }

        public BusinessException AddCode(ErrorCode code)
        {
            ValidateCode(code);
            _codes.Add(code);
            return this;
        }

        private static void ValidateCode(ErrorCode code)
        {
            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }
        }
    }

    public class ErrorMessage
    {
        public StringBuilder Message { get; } = new();
        public List<ErrorCode> ErrorCodes { get; } = new();

        public ErrorMessage Add(ErrorMessage error)
        {
            if (error is null)
            {
                throw new ArgumentNullException(nameof (error));
            }

            if (error.IsEmpty())
            {
                return this;
            }

            Message.Append(error.Message);
            ErrorCodes.AddRange(error.ErrorCodes);
            return this;
        }

        public bool IsEmpty()
        {
            return Message.Length == 0 && !ErrorCodes.Any();
        }

        public bool HasErrors()
        {
            return !IsEmpty();
        }

        public static ErrorMessage WithoutErrors
            => new();
    }

    public class ErrorCode 
    {
        public string Code { get; private set; }
        public IEnumerable<ErrorParameter> Parameters => _parameters;
        private readonly List<ErrorParameter> _parameters = new();

        public ErrorCode(string code, ErrorParameter parameter = null)
        {
            Validate(code);
            Code = code;
            if (parameter is not null)
            {
                _parameters.Add(parameter);
            }
        }

        public ErrorCode(string code, IEnumerable<ErrorParameter> parameters)
            : this(code)
        {
            foreach (var parameter in parameters)
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException($"{nameof(parameter)} is null");
                }
                _parameters.Add(parameter);
            }
        }

        public static ErrorCode Create(string code)
        {
            return new ErrorCode(code);
        }

        public static ErrorCode Create(string code, ErrorParameter errorParameter)
        {
            return new ErrorCode(code, errorParameter);
        }

        public static ErrorCode Create(string code, IEnumerable<ErrorParameter> errorParameters)
        {
            return new ErrorCode(code, errorParameters);
        }

        public ErrorCode ChangeCode(string code)
        {
            Validate(code);
            Code = code;
            return this;
        }

        public ErrorCode AddErrorParameter(ErrorParameter parameter)
        {
            _parameters.Add(parameter);
            return this;
        }

        public ErrorCode AddErrorParameter(string name, object nalue)
        {
            _parameters.Add(new ErrorParameter(name, nalue));
            return this;
        }

        private static void Validate(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentNullException(nameof(code));
            }
        }
    }

    public class ErrorParameter
    {
        public string Name { get; private set; }
        public object Value { get; private set; }

        public ErrorParameter(string name, object value)
        {
            ValidateName(name);
            ValidateValue(value);
            Name = name;
            Value = value;
        }

        public static ErrorParameter Create(string name, object value)
            => new (name, value);

        public ErrorParameter ChangeName(string name)
        {
            ValidateName(name);
            Name = name;
            return this;
        }

        public ErrorParameter ChangeValue(object value)
        {
            ValidateValue(value);
            Value = value;
            return this;
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
        }

        private static void ValidateValue(object value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }
    }
}
