namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public sealed class RegisterJobVm
    {
        public string JobName { get; set; } = default!;
        public string Schedule { get; set; } = default!;
        public string TimeZoneId { get; set; }
        public int MaxRetries { get; set; } = 3;
    }
}
