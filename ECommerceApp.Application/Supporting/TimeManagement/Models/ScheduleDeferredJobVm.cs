using System.ComponentModel.DataAnnotations;

namespace ECommerceApp.Application.Supporting.TimeManagement.Models
{
    public sealed class ScheduleDeferredJobVm
    {
        [Required]
        [MaxLength(100)]
        public string JobName { get; set; } = default!;

        [Required]
        [MaxLength(200)]
        public string EntityId { get; set; } = default!;

        [Required]
        [Range(1, 1440, ErrorMessage = "Delay must be between 1 and 1440 minutes.")]
        public int DelayMinutes { get; set; } = 5;
    }
}
