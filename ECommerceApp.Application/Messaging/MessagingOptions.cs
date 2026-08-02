namespace ECommerceApp.Application.Messaging
{
    public sealed class MessagingOptions
    {
        public bool UseBackgroundDispatcher { get; set; } = true;
        public bool CleanupEnabled { get; set; } = true;
        public System.TimeSpan OutboxRetention { get; set; } = System.TimeSpan.FromDays(7);
        public System.TimeSpan InboxRetention { get; set; } = System.TimeSpan.FromDays(7);
        public System.TimeSpan OutboxPollInterval { get; set; } = System.TimeSpan.FromSeconds(10);
        public string OutboxCleanupSchedule { get; set; } = "0 3 * * *";
        public string InboxCleanupSchedule { get; set; } = "30 3 * * *";
    }
}
