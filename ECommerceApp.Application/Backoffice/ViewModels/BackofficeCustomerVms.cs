using System.Collections.Generic;

namespace ECommerceApp.Application.Backoffice.ViewModels
{
    public sealed class BackofficeCustomerListVm
    {
        public IReadOnlyList<BackofficeCustomerItemVm> Customers { get; init; } = new List<BackofficeCustomerItemVm>();
        public int CurrentPage { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public string SearchString { get; init; }
    }

    public sealed class BackofficeCustomerItemVm
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public bool IsCompany { get; init; }
    }

    public sealed class BackofficeCustomerDetailVm
    {
        public int Id { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public bool IsCompany { get; init; }
    }
}
