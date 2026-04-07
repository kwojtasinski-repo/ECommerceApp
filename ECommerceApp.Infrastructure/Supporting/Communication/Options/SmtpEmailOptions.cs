namespace ECommerceApp.Infrastructure.Supporting.Communication.Options
{
    public sealed class SmtpEmailOptions
    {
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; } = 587;
        public string UserName { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string FromAddress { get; init; } = string.Empty;
        public string FromName { get; init; } = string.Empty;
    }
}
