using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeJobListVm
    {
        public IReadOnlyList<BackofficeJobItemVm> Jobs { get; init; } = new List<BackofficeJobItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
    }

    public sealed class BackofficeJobItemVm
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string LastRunAt { get; init; } = string.Empty;
        public string NextRunAt { get; init; } = string.Empty;
    }

    public sealed class BackofficeJobDetailVm
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string CronExpression { get; init; } = string.Empty;
        public string LastRunAt { get; init; } = string.Empty;
        public string NextRunAt { get; init; } = string.Empty;
    }
}
