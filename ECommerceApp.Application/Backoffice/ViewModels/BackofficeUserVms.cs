using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeUserListVm
    {
        public IReadOnlyList<BackofficeUserItemVm> Users { get; init; } = new List<BackofficeUserItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string SearchString { get; init; }
    }

    public sealed class BackofficeUserItemVm
    {
        public string Id { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public IReadOnlyList<string> Roles { get; init; } = new List<string>();
    }

    public sealed class BackofficeUserDetailVm
    {
        public string Id { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public IReadOnlyList<string> Roles { get; init; } = new List<string>();
    }
}
