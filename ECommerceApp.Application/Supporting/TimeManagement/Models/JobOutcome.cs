namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public abstract class JobOutcome
    {
        private protected JobOutcome() { }

        public sealed class Success : JobOutcome
        {
            public string? Message { get; }
            internal Success(string? message) { Message = message; }
        }

        public sealed class Failure : JobOutcome
        {
            public string Error { get; }
            internal Failure(string error) { Error = error; }
        }

        public sealed class Progress : JobOutcome
        {
            public string Message { get; }
            internal Progress(string message) { Message = message; }
        }

        internal static Success Succeeded(string? message = null) => new(message);
        internal static Failure Failed(string error) => new(error);
        internal static Progress InProgress(string message) => new(message);
    }
}
